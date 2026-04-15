import {
  KinesisClient,
  CreateStreamCommand,
  DescribeStreamCommand,
  DeleteStreamCommand,
  GetShardIteratorCommand,
  PutRecordCommand,
  GetRecordsCommand,
  ListShardsCommand,
  ListStreamsCommand,
  StartStreamEncryptionCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
  DescribeAccountSettingsCommand,
  UpdateAccountSettingsCommand,
  PutResourcePolicyCommand,
  GetResourcePolicyCommand,
  DeleteResourcePolicyCommand,
  UpdateMaxRecordSizeCommand,
  UpdateStreamWarmThroughputCommand,
} from '@aws-sdk/client-kinesis';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, assertErrorContains, safeCleanup } from '../../helpers.js';
import { waitForActive } from './context.js';

export async function runIndependentTests(
  runner: TestRunner,
  client: KinesisClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('kinesis', 'DescribeStream_NonExistent', async () => {
    try {
      await client.send(new DescribeStreamCommand({ StreamName: 'nonexistent-stream-xyz' }));
      throw new Error('expected ResourceNotFoundException');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('kinesis', 'DeleteStream_NonExistent', async () => {
    try {
      await client.send(new DeleteStreamCommand({ StreamName: 'nonexistent-stream-xyz' }));
      throw new Error('expected ResourceNotFoundException');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('kinesis', 'CreateStream_DuplicateName', async () => {
    const dupStream = makeUniqueName('ts-kinesis-dup');
    try {
      await client.send(new CreateStreamCommand({ StreamName: dupStream, ShardCount: 1 }));
      await assertThrows(
        () => client.send(new CreateStreamCommand({ StreamName: dupStream, ShardCount: 1 })),
        'ResourceInUseException',
      );
    } finally {
      await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: dupStream })));
    }
  }));

  results.push(await runner.runTest('kinesis', 'PutRecord_GetRecords_Roundtrip', async () => {
    const rtStream = makeUniqueName('ts-kinesis-rt');
    try {
      await client.send(new CreateStreamCommand({ StreamName: rtStream, ShardCount: 1 }));
      await waitForActive(client, rtStream);
      const testData = Buffer.from('roundtrip-kinesis-data-verify');
      const putResp = await client.send(new PutRecordCommand({
        StreamName: rtStream,
        Data: testData,
        PartitionKey: 'partition-1',
      }));
      if (!putResp.SequenceNumber) throw new Error('expected SequenceNumber to be defined');

      const descResp = await client.send(new DescribeStreamCommand({ StreamName: rtStream }));
      const shards = descResp.StreamDescription?.Shards;
      if (!shards?.length) throw new Error('no shards');

      const iterResp = await client.send(new GetShardIteratorCommand({
        StreamName: rtStream,
        ShardId: shards[0].ShardId,
        ShardIteratorType: 'TRIM_HORIZON',
      }));
      const getResp = await client.send(new GetRecordsCommand({ ShardIterator: iterResp.ShardIterator }));
      if (!getResp.Records?.length) throw new Error('no records returned');
      const rec = getResp.Records[0];
      if (Buffer.from(rec.Data ?? []).toString() !== testData.toString()) {
        throw new Error('data mismatch');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: rtStream })));
    }
  }));

  results.push(await runner.runTest('kinesis', 'ListShards_NonExistentStream', async () => {
    try {
      await client.send(new ListShardsCommand({ StreamName: 'nonexistent-stream-xyz' }));
      throw new Error('expected ResourceNotFoundException');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  const tagStreamName = makeUniqueName('ts-kinesis-tagarn');
  let tagStreamARN = '';
  try {
    await client.send(new CreateStreamCommand({ StreamName: tagStreamName, ShardCount: 1 }));
    const tagInfo = await waitForActive(client, tagStreamName);
    tagStreamARN = tagInfo.streamARN;
  } catch { /* skip tag tests */ }

  if (tagStreamARN) {
    results.push(await runner.runTest('kinesis', 'TagResource', async () => {
      await client.send(new TagResourceCommand({
        ResourceARN: tagStreamARN,
        Tags: { TagTest: 'value1', TagTest2: 'value2' },
      }));
    }));

    results.push(await runner.runTest('kinesis', 'ListTagsForResource', async () => {
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: tagStreamARN }));
      if ((resp.Tags?.length ?? 0) < 2) throw new Error(`expected >= 2 tags, got ${resp.Tags?.length ?? 0}`);
    }));

    results.push(await runner.runTest('kinesis', 'UntagResource', async () => {
      await client.send(new UntagResourceCommand({
        ResourceARN: tagStreamARN,
        TagKeys: ['TagTest'],
      }));
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: tagStreamARN }));
      if (resp.Tags?.some(t => t.Key === 'TagTest')) throw new Error('TagTest should have been removed');
    }));
  } else {
    results.push(runner.skipTest('kinesis', 'TagResource', 'tagStreamARN not available'));
    results.push(runner.skipTest('kinesis', 'ListTagsForResource', 'tagStreamARN not available'));
    results.push(runner.skipTest('kinesis', 'UntagResource', 'tagStreamARN not available'));
  }

  await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: tagStreamName })));

  results.push(await runner.runTest('kinesis', 'DescribeAccountSettings', async () => {
    await client.send(new DescribeAccountSettingsCommand({}));
  }));

  results.push(await runner.runTest('kinesis', 'UpdateAccountSettings', async () => {
    await client.send(new UpdateAccountSettingsCommand({
      MinimumThroughputBillingCommitment: { Status: 'DISABLED' },
    }));
  }));

  const policyStreamName = makeUniqueName('ts-kinesis-policy');
  let policyStreamARN = '';
  try {
    await client.send(new CreateStreamCommand({ StreamName: policyStreamName, ShardCount: 1 }));
    const policyInfo = await waitForActive(client, policyStreamName);
    policyStreamARN = policyInfo.streamARN;
  } catch { /* skip policy tests */ }

  if (policyStreamARN) {
    results.push(await runner.runTest('kinesis', 'PutResourcePolicy', async () => {
      await client.send(new PutResourcePolicyCommand({
        ResourceARN: policyStreamARN,
        Policy: '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"kinesis:*","Resource":"*"}]}',
      }));
    }));

    results.push(await runner.runTest('kinesis', 'GetResourcePolicy', async () => {
      const resp = await client.send(new GetResourcePolicyCommand({ ResourceARN: policyStreamARN }));
      if (!resp.Policy) throw new Error('expected non-empty policy');
    }));

    results.push(await runner.runTest('kinesis', 'DeleteResourcePolicy', async () => {
      await client.send(new DeleteResourcePolicyCommand({ ResourceARN: policyStreamARN }));
      const resp = await client.send(new GetResourcePolicyCommand({ ResourceARN: policyStreamARN }));
      if (resp.Policy) throw new Error(`expected empty policy after delete, got: ${resp.Policy}`);
    }));
  } else {
    results.push(runner.skipTest('kinesis', 'PutResourcePolicy', 'policyStreamARN not available'));
    results.push(runner.skipTest('kinesis', 'GetResourcePolicy', 'policyStreamARN not available'));
    results.push(runner.skipTest('kinesis', 'DeleteResourcePolicy', 'policyStreamARN not available'));
  }

  await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: policyStreamName })));

  const maxRecordStreamName = makeUniqueName('ts-kinesis-maxrec');
  try {
    await client.send(new CreateStreamCommand({ StreamName: maxRecordStreamName, ShardCount: 1 }));
    const maxRecInfo = await waitForActive(client, maxRecordStreamName);
    const maxRecARN = maxRecInfo.streamARN;

    results.push(await runner.runTest('kinesis', 'UpdateMaxRecordSize', async () => {
      await client.send(new UpdateMaxRecordSizeCommand({
        StreamARN: maxRecARN,
        MaxRecordSizeInKiB: 1024,
      }));
    }));
  } catch {
    results.push(runner.skipTest('kinesis', 'UpdateMaxRecordSize', 'stream not available'));
  }
  await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: maxRecordStreamName })));

  const warmStreamName = makeUniqueName('ts-kinesis-warm');
  try {
    await client.send(new CreateStreamCommand({ StreamName: warmStreamName, ShardCount: 1 }));
    const warmInfo = await waitForActive(client, warmStreamName);
    const warmARN = warmInfo.streamARN;

    results.push(await runner.runTest('kinesis', 'UpdateStreamWarmThroughput', async () => {
      const resp = await client.send(new UpdateStreamWarmThroughputCommand({
        StreamARN: warmARN,
        WarmThroughputMiBps: 256,
      }));
      if (!resp.WarmThroughput) throw new Error('expected WarmThroughput to be defined');
    }));
  } catch {
    results.push(runner.skipTest('kinesis', 'UpdateStreamWarmThroughput', 'stream not available'));
  }
  await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: warmStreamName })));

  results.push(await runner.runTest('kinesis', 'DescribeStream_VerifyTimestamp', async () => {
    const tsStream = makeUniqueName('ts-kinesis-ts');
    try {
      await client.send(new CreateStreamCommand({ StreamName: tsStream, ShardCount: 1 }));
      await waitForActive(client, tsStream);
      const resp = await client.send(new DescribeStreamCommand({ StreamName: tsStream }));
      const ts = resp.StreamDescription?.StreamCreationTimestamp;
      if (!ts) throw new Error('expected StreamCreationTimestamp to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: tsStream })));
    }
  }));

  results.push(await runner.runTest('kinesis', 'DescribeStream_VerifyEncryption', async () => {
    const encStream = makeUniqueName('ts-kinesis-enc');
    try {
      await client.send(new CreateStreamCommand({ StreamName: encStream, ShardCount: 1 }));
      await waitForActive(client, encStream);
      await client.send(new StartStreamEncryptionCommand({
        StreamName: encStream,
        EncryptionType: 'KMS',
        KeyId: 'arn:aws:kms:us-east-1:123456789012:key/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      }));
      const resp = await client.send(new DescribeStreamCommand({ StreamName: encStream }));
      if (resp.StreamDescription?.EncryptionType !== 'KMS') {
        throw new Error(`expected KMS encryption, got ${resp.StreamDescription?.EncryptionType}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: encStream })));
    }
  }));

  results.push(await runner.runTest('kinesis', 'ListTagsForResource_StreamCreated', async () => {
    const tlcStream = makeUniqueName('ts-kinesis-tlcr');
    try {
      await client.send(new CreateStreamCommand({
        StreamName: tlcStream,
        ShardCount: 1,
        Tags: { CreatedBy: 'sdk-test', Project: 'vorpalstacks' },
      }));
      await waitForActive(client, tlcStream);
      const descResp = await client.send(new DescribeStreamCommand({ StreamName: tlcStream }));
      const arn = descResp.StreamDescription?.StreamARN;
      if (!arn) throw new Error('expected StreamARN to be defined');
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: arn }));
      if ((resp.Tags?.length ?? 0) < 2) {
        throw new Error(`expected >= 2 tags from stream creation, got ${resp.Tags?.length ?? 0}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteStreamCommand({ StreamName: tlcStream })));
    }
  }));

  results.push(await runner.runTest('kinesis', 'ListStreams_Pagination', async () => {
    const pgPrefix = `PagStr-${Date.now()}`;
    const pgStreams = Array.from({ length: 5 }, (_, i) => `${pgPrefix}-${i}`);

    for (const name of pgStreams) {
      await client.send(new CreateStreamCommand({ StreamName: name, ShardCount: 1 }));
    }

    try {
      const allFound: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListStreamsCommand({ Limit: 2, NextToken: nextToken }));
        for (const sn of resp.StreamNames ?? []) {
          if (sn.startsWith('PagStr-')) allFound.push(sn);
        }
        nextToken = resp.NextToken;
      } while (nextToken);

      const matching = allFound.filter(sn => pgStreams.includes(sn));
      if (matching.length !== 5) {
        throw new Error(`expected 5 paginated streams, got ${matching.length}`);
      }
    } finally {
      for (const name of pgStreams) {
        await safeCleanup(() => client.send(new DeleteStreamCommand({
          StreamName: name,
          EnforceConsumerDeletion: true,
        })));
      }
    }
  }));

  return results;
}
