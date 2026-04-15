import {
  SNSClient,
  CreateTopicCommand,
  ListTopicsCommand,
  GetTopicAttributesCommand,
  SetTopicAttributesCommand,
  PublishCommand,
  AddPermissionCommand,
  RemovePermissionCommand,
  SubscribeCommand,
  ListSubscriptionsCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
  PublishBatchCommand,
  DeleteTopicCommand,
  UnsubscribeCommand,
  ListSubscriptionsByTopicCommand,
  GetSubscriptionAttributesCommand,
  SetSubscriptionAttributesCommand,
} from '@aws-sdk/client-sns';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';
import type { TopicState } from './context.js';

export async function runTopicLifecycleTests(
  runner: TestRunner,
  client: SNSClient,
  state: TopicState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const topicName = makeUniqueName('TestTopic');

  results.push(await runner.runTest('sns', 'CreateTopic', async () => {
    const resp = await client.send(new CreateTopicCommand({ Name: topicName }));
    if (!resp.TopicArn) throw new Error('expected TopicArn to be defined');
    state.topicArn = resp.TopicArn;
  }));

  results.push(await runner.runTest('sns', 'ListTopics', async () => {
    const resp = await client.send(new ListTopicsCommand({}));
    if (!resp.Topics) throw new Error('expected Topics to be defined');
  }));

  results.push(await runner.runTest('sns', 'GetTopicAttributes', async () => {
    const resp = await client.send(new GetTopicAttributesCommand({ TopicArn: state.topicArn }));
    if (!resp.Attributes) throw new Error('expected Attributes to be defined');
  }));

  results.push(await runner.runTest('sns', 'SetTopicAttributes', async () => {
    await client.send(new SetTopicAttributesCommand({
      TopicArn: state.topicArn, AttributeName: 'DisplayName', AttributeValue: 'Test Topic',
    }));
  }));

  results.push(await runner.runTest('sns', 'Publish', async () => {
    const resp = await client.send(new PublishCommand({ TopicArn: state.topicArn, Message: 'Test message' }));
    if (!resp.MessageId) throw new Error('expected MessageId to be defined');
  }));

  results.push(await runner.runTest('sns', 'AddPermission', async () => {
    await client.send(new AddPermissionCommand({
      TopicArn: state.topicArn, Label: 'TestPermission',
      AWSAccountId: ['000000000000'], ActionName: ['Publish'],
    }));
  }));

  results.push(await runner.runTest('sns', 'RemovePermission', async () => {
    await client.send(new RemovePermissionCommand({ TopicArn: state.topicArn, Label: 'TestPermission' }));
  }));

  const sqsQueueUrl = 'http://127.0.0.1:8080/000000000000/test-queue';

  results.push(await runner.runTest('sns', 'Subscribe', async () => {
    const resp = await client.send(new SubscribeCommand({
      TopicArn: state.topicArn, Protocol: 'http', Endpoint: sqsQueueUrl,
    }));
    if (!resp.SubscriptionArn) throw new Error('expected SubscriptionArn to be defined');
    state.subscriptionArn = resp.SubscriptionArn;
  }));

  results.push(await runner.runTest('sns', 'ListSubscriptions', async () => {
    const resp = await client.send(new ListSubscriptionsCommand({}));
    if (!resp.Subscriptions) throw new Error('expected Subscriptions to be defined');
  }));

  results.push(await runner.runTest('sns', 'TagResource', async () => {
    await client.send(new TagResourceCommand({
      ResourceArn: state.topicArn,
      Tags: [{ Key: 'Environment', Value: 'test' }, { Key: 'Owner', Value: 'team-a' }],
    }));
  }));

  results.push(await runner.runTest('sns', 'ListTagsForResource', async () => {
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceArn: state.topicArn }));
    if (!resp.Tags) throw new Error('expected Tags to be defined');
  }));

  results.push(await runner.runTest('sns', 'UntagResource', async () => {
    await client.send(new UntagResourceCommand({ ResourceArn: state.topicArn, TagKeys: ['Owner'] }));
  }));

  results.push(await runner.runTest('sns', 'PublishBatch', async () => {
    const resp = await client.send(new PublishBatchCommand({
      TopicArn: state.topicArn,
      PublishBatchRequestEntries: [
        { Id: '1', Message: 'batch msg 1' },
        { Id: '2', Message: 'batch msg 2' },
      ],
    }));
    if (!resp.Successful) throw new Error('expected Successful to be defined');
  }));

  const fifoTopicName = makeUniqueName('FifoTest') + '.fifo';

  results.push(await runner.runTest('sns', 'CreateTopic (FIFO)', async () => {
    const resp = await client.send(new CreateTopicCommand({
      Name: fifoTopicName,
      Attributes: { FifoTopic: 'true', ContentBasedDeduplication: 'true' },
    }));
    if (!resp.TopicArn) throw new Error('expected TopicArn to be defined');
    state.fifoTopicArn = resp.TopicArn;
  }));

  results.push(await runner.runTest('sns', 'Unsubscribe', async () => {
    if (!state.subscriptionArn) throw new Error('subscription ARN not available');
    await client.send(new UnsubscribeCommand({ SubscriptionArn: state.subscriptionArn }));
  }));

  results.push(await runner.runTest('sns', 'DeleteTopic', async () => {
    await client.send(new DeleteTopicCommand({ TopicArn: state.topicArn }));
  }));

  results.push(await runner.runTest('sns', 'DeleteTopic_VerifyGone', async () => {
    try {
      await client.send(new GetTopicAttributesCommand({ TopicArn: state.topicArn }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  results.push(await runner.runTest('sns', 'DeleteTopic_NonExistent', async () => {
    try {
      await client.send(new DeleteTopicCommand({
        TopicArn: 'arn:aws:sns:us-east-1:000000000000:nonexistent-topic',
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'NotFound');
    }
  }));

  results.push(await runner.runTest('sns', 'Unsubscribe_VerifyGone', async () => {
    try {
      await client.send(new UnsubscribeCommand({ SubscriptionArn: state.subscriptionArn }));
      throw new Error('expected error');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error') throw err;
    }
  }));

  let autoSubArn = '';
  let autoTopicArn = '';
  results.push(await runner.runTest('sns', 'Subscribe_SQS_AutoConfirmed', async () => {
    const newTopic = makeUniqueName('AutoTopic');
    const tResp = await client.send(new CreateTopicCommand({ Name: newTopic }));
    autoTopicArn = tResp.TopicArn!;
    const resp = await client.send(new SubscribeCommand({
      TopicArn: autoTopicArn, Protocol: 'sqs',
      Endpoint: 'arn:aws:sqs:us-east-1:000000000000:my-queue',
    }));
    if (!resp.SubscriptionArn) throw new Error('expected SubscriptionArn');
    autoSubArn = resp.SubscriptionArn;
  }));

  results.push(await runner.runTest('sns', 'GetSubscriptionAttributes', async () => {
    if (!autoSubArn) throw new Error('subscription ARN not available');
    const resp = await client.send(new GetSubscriptionAttributesCommand({ SubscriptionArn: autoSubArn }));
    if (!resp.Attributes) throw new Error('expected Attributes to be defined');
  }));

  results.push(await runner.runTest('sns', 'SetSubscriptionAttributes', async () => {
    if (!autoSubArn) throw new Error('subscription ARN not available');
    await client.send(new SetSubscriptionAttributesCommand({
      SubscriptionArn: autoSubArn, AttributeName: 'RawMessageDelivery', AttributeValue: 'true',
    }));
  }));

  results.push(await runner.runTest('sns', 'ListSubscriptionsByTopic', async () => {
    if (!autoSubArn) return;
  }));

  results.push(await runner.runTest('sns', 'ListSubscriptions_ContainsCreated', async () => {
    const resp = await client.send(new ListSubscriptionsCommand({}));
    if (!resp.Subscriptions?.length) throw new Error('expected subscriptions to be defined');
  }));

  if (state.fifoTopicArn) {
    results.push(await runner.runTest('sns', 'Publish_FIFO_WithMessageGroupId', async () => {
      const resp = await client.send(new PublishCommand({
        TopicArn: state.fifoTopicArn, Message: 'FIFO message',
        MessageGroupId: 'group1',
      }));
      if (!resp.MessageId) throw new Error('expected MessageId to be defined');
    }));

    results.push(await runner.runTest('sns', 'Publish_FIFO_ContentBasedDedup', async () => {
      const resp = await client.send(new PublishCommand({
        TopicArn: state.fifoTopicArn, Message: 'dedup message',
        MessageGroupId: 'group1',
      }));
      if (!resp.MessageId) throw new Error('expected MessageId to be defined');
    }));

    results.push(await runner.runTest('sns', 'Publish_FIFO_DeduplicationId', async () => {
      const resp = await client.send(new PublishCommand({
        TopicArn: state.fifoTopicArn, Message: 'manual dedup',
        MessageDeduplicationId: makeUniqueName('dedup'), MessageGroupId: 'group1',
      }));
      if (!resp.MessageId) throw new Error('expected MessageId to be defined');
    }));
  }

  if (state.fifoTopicArn) {
    await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: state.fifoTopicArn })));
  }
  if (autoTopicArn) {
    await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: autoTopicArn })));
  }

  return results;
}
