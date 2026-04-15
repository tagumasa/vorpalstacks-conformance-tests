import {
  SQSClient,
  CreateQueueCommand,
  GetQueueUrlCommand,
  GetQueueAttributesCommand,
  SetQueueAttributesCommand,
  SendMessageCommand,
  ReceiveMessageCommand,
  DeleteMessageCommand,
  ListQueuesCommand,
  DeleteQueueCommand,
  TagQueueCommand,
  ListQueueTagsCommand,
  UntagQueueCommand,
} from '@aws-sdk/client-sqs';
import type { TestRunner, TestResult } from '../../runner.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

async function getQueueUrl(client: SQSClient, queueName: string): Promise<string> {
  const resp = await client.send(new GetQueueUrlCommand({ QueueName: queueName }));
  if (!resp.QueueUrl) throw new Error('expected QueueUrl to be defined');
  return resp.QueueUrl;
}

async function cleanupQueue(client: SQSClient, queueName: string): Promise<void> {
  try {
    const url = await getQueueUrl(client, queueName);
    await client.send(new DeleteQueueCommand({ QueueUrl: url }));
  } catch { /* queue doesn't exist */ }
}

export async function runQueueCrudTests(
  runner: TestRunner,
  client: SQSClient,
  ts: string,
  queueName: string,
): Promise<{ results: TestResult[]; qUrl: string }> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sqs', 'CreateQueue', async () => {
    const resp = await client.send(new CreateQueueCommand({ QueueName: queueName }));
    if (!resp.QueueUrl) throw new Error('CreateQueue returned nil QueueUrl');
  }));

  results.push(await runner.runTest('sqs', 'GetQueueUrl', async () => {
    const resp = await client.send(new GetQueueUrlCommand({ QueueName: queueName }));
    if (!resp.QueueUrl) throw new Error('GetQueueUrl returned nil QueueUrl');
  }));

  const qUrl = await getQueueUrl(client, queueName);

  results.push(await runner.runTest('sqs', 'GetQueueAttributes', async () => {
    const resp = await client.send(new GetQueueAttributesCommand({ QueueUrl: qUrl }));
    if (!resp.Attributes) throw new Error('GetQueueAttributes returned nil Attributes');
  }));

  results.push(await runner.runTest('sqs', 'GetQueueAttributes_SpecificAttributes', async () => {
    const resp = await client.send(new GetQueueAttributesCommand({
      QueueUrl: qUrl,
      AttributeNames: ['QueueArn', 'VisibilityTimeout'],
    }));
    if (!resp.Attributes) throw new Error('GetQueueAttributes returned nil Attributes');
    if (!resp.Attributes['QueueArn']) throw new Error('GetQueueAttributes missing QueueArn');
    if (!resp.Attributes['VisibilityTimeout']) throw new Error('GetQueueAttributes missing VisibilityTimeout');
  }));

  results.push(await runner.runTest('sqs', 'SendMessage', async () => {
    const resp = await client.send(new SendMessageCommand({ QueueUrl: qUrl, MessageBody: 'Test message' }));
    if (!resp.MessageId || resp.MessageId === '') throw new Error('SendMessage returned nil or empty MessageId');
  }));

  results.push(await runner.runTest('sqs', 'SendMessage_WithDelaySeconds', async () => {
    const resp = await client.send(new SendMessageCommand({ QueueUrl: qUrl, MessageBody: 'Delayed message', DelaySeconds: 5 }));
    if (!resp.MessageId || resp.MessageId === '') throw new Error('SendMessage with DelaySeconds returned nil MessageId');
  }));

  results.push(await runner.runTest('sqs', 'SendMessage_WithMessageAttributes', async () => {
    const attrQueueName = `AttrQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: attrQueueName }));
    try {
      const attrUrl = await getQueueUrl(client, attrQueueName);
      await client.send(new SendMessageCommand({
        QueueUrl: attrUrl,
        MessageBody: 'Message with attributes',
        MessageAttributes: {
          Attr1: { DataType: 'String', StringValue: 'value1' },
          Attr2: { DataType: 'Number', StringValue: '42' },
        },
      }));
      const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: attrUrl, MessageAttributeNames: ['All'] }));
      if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received');
      const msg = recvResp.Messages[0];
      if (!msg.MessageAttributes || Object.keys(msg.MessageAttributes).length < 2) {
        throw new Error(`expected at least 2 message attributes, got ${Object.keys(msg.MessageAttributes ?? {}).length}`);
      }
      if (msg.MessageAttributes['Attr1']?.StringValue !== 'value1') throw new Error('Attr1 mismatch');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, attrQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'ReceiveMessage', async () => {
    const resp = await client.send(new ReceiveMessageCommand({ QueueUrl: qUrl }));
    if (!resp.Messages || resp.Messages.length === 0) throw new Error('ReceiveMessage returned empty Messages list');
  }));

  results.push(await runner.runTest('sqs', 'ReceiveMessage_MaxNumberOfMessages', async () => {
    const rtQueueName = `RMNQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: rtQueueName }));
    try {
      const rtUrl = await getQueueUrl(client, rtQueueName);
      for (const _ of [0, 1, 2, 3, 4]) {
        await client.send(new SendMessageCommand({ QueueUrl: rtUrl, MessageBody: `msg-${_}` }));
      }
      const resp = await client.send(new ReceiveMessageCommand({ QueueUrl: rtUrl, MaxNumberOfMessages: 5 }));
      if (!resp.Messages || resp.Messages.length < 5) throw new Error(`expected at least 5 messages, got ${resp.Messages?.length}`);
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, rtQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'ReceiveMessage_WaitTimeSeconds', async () => {
    await client.send(new ReceiveMessageCommand({ QueueUrl: qUrl, WaitTimeSeconds: 1 }));
  }));

  results.push(await runner.runTest('sqs', 'ReceiveMessage_VisibilityTimeout', async () => {
    const rtQueueName = `RVTQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: rtQueueName }));
    try {
      const rtUrl = await getQueueUrl(client, rtQueueName);
      await client.send(new SendMessageCommand({ QueueUrl: rtUrl, MessageBody: 'visibility-test-msg' }));
      const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: rtUrl, VisibilityTimeout: 120 }));
      if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received');
      const recvResp2 = await client.send(new ReceiveMessageCommand({ QueueUrl: rtUrl }));
      if (recvResp2.Messages && recvResp2.Messages.length > 0) {
        throw new Error('message should be invisible after 120s visibility timeout');
      }
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, rtQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'DeleteMessage', async () => {
    await client.send(new SendMessageCommand({ QueueUrl: qUrl, MessageBody: 'Message to delete' }));
    const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: qUrl }));
    if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received for DeleteMessage test');
    await client.send(new DeleteMessageCommand({ QueueUrl: qUrl, ReceiptHandle: recvResp.Messages[0].ReceiptHandle! }));
  }));

  results.push(await runner.runTest('sqs', 'DeleteMessage_NonExistent', async () => {
    try {
      await client.send(new DeleteMessageCommand({
        QueueUrl: 'https://queue.amazonaws.com/000000000000/nonexistent',
        ReceiptHandle: 'fake-receipt-handle',
      }));
      throw new Error('expected error for non-existent message');
    } catch (err) {
      assertErrorContains(err, 'ReceiptHandleIsInvalid');
    }
  }));

  results.push(await runner.runTest('sqs', 'ListQueues', async () => {
    const resp = await client.send(new ListQueuesCommand({}));
    if (!resp.QueueUrls) throw new Error('ListQueues returned nil QueueUrls');
  }));

  results.push(await runner.runTest('sqs', 'ListQueues_WithPrefix', async () => {
    const prefix = `PrefixTest-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: prefix + '-alpha' }));
    await client.send(new CreateQueueCommand({ QueueName: prefix + '-beta' }));
    try {
      const resp = await client.send(new ListQueuesCommand({ QueueNamePrefix: prefix }));
      if (!resp.QueueUrls || resp.QueueUrls.length < 2) throw new Error(`expected at least 2 queues, got ${resp.QueueUrls?.length}`);
      for (const u of resp.QueueUrls) {
        if (!u.includes(prefix)) throw new Error(`ListQueues returned URL not matching prefix: ${u}`);
      }
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, prefix + '-alpha'); });
      await safeCleanup(async () => { await cleanupQueue(client, prefix + '-beta'); });
    }
  }));

  results.push(await runner.runTest('sqs', 'SetQueueAttributes', async () => {
    await client.send(new SetQueueAttributesCommand({ QueueUrl: qUrl, Attributes: { VisibilityTimeout: '30' } }));
  }));

  results.push(await runner.runTest('sqs', 'SetQueueAttributes_MultipleAttrs', async () => {
    const smaQueueName = `SMAQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: smaQueueName }));
    try {
      const smaUrl = await getQueueUrl(client, smaQueueName);
      await client.send(new SetQueueAttributesCommand({
        QueueUrl: smaUrl,
        Attributes: { VisibilityTimeout: '45', MaximumMessageSize: '1024', MessageRetentionPeriod: '345600', DelaySeconds: '10' },
      }));
      const attrResp = await client.send(new GetQueueAttributesCommand({
        QueueUrl: smaUrl,
        AttributeNames: ['VisibilityTimeout', 'MaximumMessageSize', 'MessageRetentionPeriod', 'DelaySeconds'],
      }));
      if (attrResp.Attributes?.VisibilityTimeout !== '45') throw new Error('VisibilityTimeout mismatch');
      if (attrResp.Attributes?.MaximumMessageSize !== '1024') throw new Error('MaximumMessageSize mismatch');
      if (attrResp.Attributes?.MessageRetentionPeriod !== '345600') throw new Error('MessageRetentionPeriod mismatch');
      if (attrResp.Attributes?.DelaySeconds !== '10') throw new Error('DelaySeconds mismatch');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, smaQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'TagQueue', async () => {
    await client.send(new TagQueueCommand({ QueueUrl: qUrl, Tags: { Environment: 'Test' } }));
  }));

  results.push(await runner.runTest('sqs', 'ListQueueTags', async () => {
    const resp = await client.send(new ListQueueTagsCommand({ QueueUrl: qUrl }));
    if (!resp.Tags || Object.keys(resp.Tags).length === 0) throw new Error('ListQueueTags returned nil or empty Tags');
  }));

  results.push(await runner.runTest('sqs', 'UntagQueue', async () => {
    await client.send(new UntagQueueCommand({ QueueUrl: qUrl, TagKeys: ['Environment'] }));
  }));

  return { results, qUrl };
}
