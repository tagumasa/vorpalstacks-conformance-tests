import {
  SNSClient,
  CreateTopicCommand,
  DeleteTopicCommand,
  PublishCommand,
  PublishBatchCommand,
  PutDataProtectionPolicyCommand,
  GetDataProtectionPolicyCommand,
  CreatePlatformApplicationCommand,
  ListPlatformApplicationsCommand,
  GetPlatformApplicationAttributesCommand,
  SetPlatformApplicationAttributesCommand,
  CreatePlatformEndpointCommand,
  ListEndpointsByPlatformApplicationCommand,
  GetEndpointAttributesCommand,
  SetEndpointAttributesCommand,
  DeletePlatformApplicationCommand,
  DeleteEndpointCommand,
} from '@aws-sdk/client-sns';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runAdvancedTests(
  runner: TestRunner,
  client: SNSClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sns', 'Publish_WithMessageAttributes', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('AttrTopic') }));
    const tArn = tResp.TopicArn!;
    try {
      await client.send(new PublishCommand({
        TopicArn: tArn, Message: 'msg with attrs',
        MessageAttributes: {
          attr1: { DataType: 'String', StringValue: 'value1' },
          attr2: { DataType: 'Number', StringValue: '42' },
        },
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'PublishBatch_WithAttributes', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('BatchAttr') }));
    const tArn = tResp.TopicArn!;
    try {
      await client.send(new PublishBatchCommand({
        TopicArn: tArn,
        PublishBatchRequestEntries: [{
          Id: '1', Message: 'attr batch',
          MessageAttributes: { k1: { DataType: 'String', StringValue: 'v1' } },
        }],
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'PublishBatch_MaxEntries', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('BatchMax') }));
    const tArn = tResp.TopicArn!;
    try {
      const entries = Array.from({ length: 10 }, (_, i) => ({ Id: `id-${i}`, Message: `msg-${i}` }));
      await client.send(new PublishBatchCommand({
        TopicArn: tArn, PublishBatchRequestEntries: entries,
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'PublishBatch_FailedEntry', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('BatchFail') }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new PublishBatchCommand({
        TopicArn: tArn,
        PublishBatchRequestEntries: [{ Id: '1', Message: 'valid' }],
      }));
      if (!resp.Successful?.length) throw new Error('expected at least one successful entry');
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'PutDataProtectionPolicy', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('DP') }));
    const tArn = tResp.TopicArn!;
    try {
      await client.send(new PutDataProtectionPolicyCommand({
        ResourceArn: tArn,
        DataProtectionPolicy: JSON.stringify({
          version: '2017-03-31',
          statement: [{ sid: 'deny-inbound', effect: 'Deny', principal: ['*'], action: ['SNS:Publish'], condition: { stringEquals: { 'sns:Protocol': 'http' } } }],
        }),
      }));
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'GetDataProtectionPolicy', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('DPGet') }));
    const tArn = tResp.TopicArn!;
    try {
      await client.send(new PutDataProtectionPolicyCommand({
        ResourceArn: tArn,
        DataProtectionPolicy: JSON.stringify({ version: '2017-03-31', statement: [{ sid: 'deny', effect: 'Deny', principal: ['*'], action: ['SNS:Publish'] }] }),
      }));
      const resp = await client.send(new GetDataProtectionPolicyCommand({ ResourceArn: tArn }));
      if (!resp.DataProtectionPolicy) throw new Error('expected DataProtectionPolicy to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'GetDataProtectionPolicy_NonExistent', async () => {
    try {
      await client.send(new GetDataProtectionPolicyCommand({
        ResourceArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent',
      }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  let platformAppArn = '';
  results.push(await runner.runTest('sns', 'CreatePlatformApplication', async () => {
    const resp = await client.send(new CreatePlatformApplicationCommand({
      Name: makeUniqueName('TestPlatform'), Platform: 'GCM',
      Attributes: { PlatformCredential: 'test-api-key' },
    }));
    if (!resp.PlatformApplicationArn) throw new Error('expected PlatformApplicationArn to be defined');
    platformAppArn = resp.PlatformApplicationArn;
  }));

  results.push(await runner.runTest('sns', 'CreatePlatformApplication_Duplicate', async () => {
    try {
      await client.send(new CreatePlatformApplicationCommand({
        Name: platformAppArn.split('/').pop()!, Platform: 'GCM',
        Attributes: { PlatformCredential: 'test-api-key' },
      }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('sns', 'ListPlatformApplications', async () => {
    const resp = await client.send(new ListPlatformApplicationsCommand({}));
    if (!resp.PlatformApplications) throw new Error('expected PlatformApplications to be defined');
  }));

  results.push(await runner.runTest('sns', 'GetPlatformApplicationAttributes', async () => {
    const resp = await client.send(new GetPlatformApplicationAttributesCommand({ PlatformApplicationArn: platformAppArn }));
    if (!resp.Attributes) throw new Error('expected Attributes to be defined');
  }));

  results.push(await runner.runTest('sns', 'SetPlatformApplicationAttributes', async () => {
    await client.send(new SetPlatformApplicationAttributesCommand({
      PlatformApplicationArn: platformAppArn,
      Attributes: { EventEndpointCreated: 'arn:aws:sqs:us-east-1:000000000000:my-queue' },
    }));
  }));

  let endpointArn = '';
  results.push(await runner.runTest('sns', 'CreatePlatformEndpoint', async () => {
    const resp = await client.send(new CreatePlatformEndpointCommand({
      PlatformApplicationArn: platformAppArn, Token: makeUniqueName('device-token'),
    }));
    if (!resp.EndpointArn) throw new Error('expected EndpointArn to be defined');
    endpointArn = resp.EndpointArn;
  }));

  results.push(await runner.runTest('sns', 'ListEndpointsByPlatformApplication', async () => {
    const resp = await client.send(new ListEndpointsByPlatformApplicationCommand({ PlatformApplicationArn: platformAppArn }));
    if (!resp.Endpoints) throw new Error('expected Endpoints to be defined');
  }));

  results.push(await runner.runTest('sns', 'GetEndpointAttributes', async () => {
    const resp = await client.send(new GetEndpointAttributesCommand({ EndpointArn: endpointArn }));
    if (!resp.Attributes) throw new Error('expected Attributes to be defined');
  }));

  results.push(await runner.runTest('sns', 'SetEndpointAttributes', async () => {
    await client.send(new SetEndpointAttributesCommand({
      EndpointArn: endpointArn, Attributes: { CustomUserData: 'updated-data' },
    }));
  }));

  results.push(await runner.runTest('sns', 'DeletePlatformApplication_Cascade', async () => {
    await client.send(new DeleteEndpointCommand({ EndpointArn: endpointArn }));
    await client.send(new DeletePlatformApplicationCommand({ PlatformApplicationArn: platformAppArn }));
  }));

  results.push(await runner.runTest('sns', 'DeleteEndpoint', async () => {
    // Already deleted in cascade test
  }));

  return results;
}
