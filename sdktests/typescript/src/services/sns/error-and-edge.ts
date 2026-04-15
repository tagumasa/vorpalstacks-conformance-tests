import {
  SNSClient,
  CreateTopicCommand,
  DeleteTopicCommand,
  ListTopicsCommand,
  GetTopicAttributesCommand,
  SetTopicAttributesCommand,
  PublishCommand,
  SubscribeCommand,
  UnsubscribeCommand,
  GetSubscriptionAttributesCommand,
  SetSubscriptionAttributesCommand,
  ListSubscriptionsByTopicCommand,
  ListTagsForResourceCommand,
  TagResourceCommand,
} from '@aws-sdk/client-sns';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';
import type { TopicState } from './context.js';

export async function runErrorAndEdgeTests(
  runner: TestRunner,
  client: SNSClient,
  state: TopicState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sns', 'GetTopicAttributes_NonExistent', async () => {
    try {
      await client.send(new GetTopicAttributesCommand({ TopicArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent' }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'Publish_NonExistentTopic', async () => {
    try {
      await client.send(new PublishCommand({ TopicArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent', Message: 'test' }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'CreateTopic_DuplicateIdempotent', async () => {
    const dupName = makeUniqueName('Idempotent');
    await client.send(new CreateTopicCommand({ Name: dupName }));
    const resp2 = await client.send(new CreateTopicCommand({ Name: dupName }));
    if (!resp2.TopicArn) throw new Error('expected TopicArn on duplicate create');
  }));

  results.push(await runner.runTest('sns', 'ListTopics_ContainsCreated', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('ListContains') }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new ListTopicsCommand({}));
      if (!resp.Topics?.some(t => t.TopicArn === tArn)) {
        throw new Error('created topic not found in ListTopics');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'SetTopicAttributes_GetVerify', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('SetVerify') }));
    const tArn = tResp.TopicArn!;
    try {
      await client.send(new SetTopicAttributesCommand({
        TopicArn: tArn, AttributeName: 'DisplayName', AttributeValue: 'Verified Name',
      }));
      const resp = await client.send(new GetTopicAttributesCommand({ TopicArn: tArn }));
      if (resp.Attributes?.DisplayName !== 'Verified Name') {
        throw new Error(`DisplayName mismatch: got ${resp.Attributes?.DisplayName}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'Subscribe_NonExistentTopic', async () => {
    try {
      await client.send(new SubscribeCommand({
        TopicArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent',
        Protocol: 'http', Endpoint: 'http://example.com',
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'Unsubscribe_NonExistent', async () => {
    try {
      await client.send(new UnsubscribeCommand({
        SubscriptionArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent:sub',
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'SetSubscriptionAttributes_NonExistent', async () => {
    try {
      await client.send(new SetSubscriptionAttributesCommand({
        SubscriptionArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent:sub',
        AttributeName: 'RawMessageDelivery', AttributeValue: 'true',
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'GetSubscriptionAttributes_NonExistent', async () => {
    try {
      await client.send(new GetSubscriptionAttributesCommand({
        SubscriptionArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent:sub',
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'SetTopicAttributes_NonExistent', async () => {
    try {
      await client.send(new SetTopicAttributesCommand({
        TopicArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent',
        AttributeName: 'DisplayName', AttributeValue: 'test',
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'Subscribe_EmailPendingConfirmation', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('EmailTopic') }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new SubscribeCommand({
        TopicArn: tArn, Protocol: 'email', Endpoint: 'test@example.com',
      }));
      if (!resp.SubscriptionArn) throw new Error('expected SubscriptionArn');
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'Subscribe_ApplicationPendingConfirmation', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('AppTopic') }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new SubscribeCommand({
        TopicArn: tArn, Protocol: 'application',
        Endpoint: 'arn:aws:sns:us-east-1:000000000000:app/MyApp/endpoint123',
      }));
      if (!resp.SubscriptionArn) throw new Error('expected SubscriptionArn');
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'GetTopicAttributes_FifoAttributes', async () => {
    const fifoAttrName = makeUniqueName('FifoAttrTopic') + '.fifo';
    const tResp = await client.send(new CreateTopicCommand({
      Name: fifoAttrName,
      Attributes: { FifoTopic: 'true', ContentBasedDeduplication: 'true' },
    }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new GetTopicAttributesCommand({ TopicArn: tArn }));
      if (resp.Attributes?.FifoTopic !== 'true') throw new Error('expected FifoTopic=true');
      if (resp.Attributes?.ContentBasedDeduplication !== 'true') throw new Error('expected ContentBasedDeduplication=true');
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'GetTopicAttributes_PolicyDefault', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('PolicyTopic') }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new GetTopicAttributesCommand({ TopicArn: tArn }));
      if (!resp.Attributes?.Policy) throw new Error('expected default Policy to be defined');
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'CreateTopic_WithTags', async () => {
    const resp = await client.send(new CreateTopicCommand({
      Name: makeUniqueName('TaggedTopic'),
      Tags: [{ Key: 'Team', Value: 'dev' }],
    }));
    const tArn = resp.TopicArn!;
    try {
      const tagResp = await client.send(new ListTagsForResourceCommand({ ResourceArn: tArn }));
      if (!tagResp.Tags?.some(t => t.Key === 'Team' && t.Value === 'dev')) {
        throw new Error('tag not found on created topic');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'TagResource_MultipleTags', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('MultiTag') }));
    const tArn = tResp.TopicArn!;
    try {
      await client.send(new TagResourceCommand({
        ResourceArn: tArn,
        Tags: [{ Key: 'A', Value: '1' }, { Key: 'B', Value: '2' }, { Key: 'C', Value: '3' }],
      }));
      const tagResp = await client.send(new ListTagsForResourceCommand({ ResourceArn: tArn }));
      if (tagResp.Tags?.length !== 3) throw new Error(`expected 3 tags, got ${tagResp.Tags?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'ListSubscriptionsByTopic_Empty', async () => {
    const tResp = await client.send(new CreateTopicCommand({ Name: makeUniqueName('EmptySub') }));
    const tArn = tResp.TopicArn!;
    try {
      const resp = await client.send(new ListSubscriptionsByTopicCommand({ TopicArn: tArn }));
      if (resp.Subscriptions?.length) {
        throw new Error(`expected no subscriptions, got ${resp.Subscriptions.length}`);
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: tArn })));
    }
  }));

  results.push(await runner.runTest('sns', 'ListTopics_Pagination', async () => {
    const pgTs = makeUniqueName('pg');
    const topicArns: string[] = [];
    try {
      for (const suffix of ['0', '1', '2', '3', '4']) {
        const resp = await client.send(new CreateTopicCommand({ Name: `${pgTs}-${suffix}` }));
        topicArns.push(resp.TopicArn!);
      }
      const found: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListTopicsCommand({ NextToken: nextToken }));
        for (const t of resp.Topics ?? []) {
          if (t.TopicArn?.includes(pgTs)) found.push(t.TopicArn);
        }
        nextToken = resp.NextToken;
      } while (nextToken);
      if (found.length !== 5) throw new Error(`expected 5 paginated topics, got ${found.length}`);
    } finally {
      for (const arn of topicArns) {
        await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: arn })));
      }
    }
  }));

  return results;
}
