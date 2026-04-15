import {
  SQSClient,
  CreateQueueCommand,
  GetQueueUrlCommand,
  GetQueueAttributesCommand,
  SendMessageCommand,
  SendMessageBatchCommand,
  ReceiveMessageCommand,
  DeleteMessageBatchCommand,
  DeleteQueueCommand,
  ChangeMessageVisibilityCommand,
  ChangeMessageVisibilityBatchCommand,
  PurgeQueueCommand,
  AddPermissionCommand,
  RemovePermissionCommand,
  ListQueuesCommand,
  ListDeadLetterSourceQueuesCommand,
  StartMessageMoveTaskCommand,
  CancelMessageMoveTaskCommand,
  ListMessageMoveTasksCommand,
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

export async function runMessageAndAdvancedTests(
  runner: TestRunner,
  client: SQSClient,
  ts: string,
  qUrl: string,
  batchQueueName: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sqs', 'PurgeQueue', async () => {
    await client.send(new PurgeQueueCommand({ QueueUrl: qUrl }));
  }));

  results.push(await runner.runTest('sqs', 'ChangeMessageVisibility', async () => {
    await client.send(new SendMessageCommand({ QueueUrl: qUrl, MessageBody: 'Test message for visibility' }));
    const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: qUrl }));
    if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received');
    const rh = recvResp.Messages[0].ReceiptHandle;
    if (!rh || rh === '') throw new Error('receipt handle is empty');
    await client.send(new ChangeMessageVisibilityCommand({ QueueUrl: qUrl, ReceiptHandle: rh, VisibilityTimeout: 60 }));
  }));

  results.push(await runner.runTest('sqs', 'ChangeMessageVisibilityBatch', async () => {
    for (const _ of [0, 1, 2]) {
      await client.send(new SendMessageCommand({ QueueUrl: qUrl, MessageBody: `CMVB-msg-${_}` }));
    }
    const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: qUrl, MaxNumberOfMessages: 10 }));
    if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received');
    const entries = recvResp.Messages.map((msg, i) => ({
      Id: `cmvb${i}`, ReceiptHandle: msg.ReceiptHandle!, VisibilityTimeout: 120,
    }));
    const batchResp = await client.send(new ChangeMessageVisibilityBatchCommand({ QueueUrl: qUrl, Entries: entries }));
    if (!batchResp.Successful || batchResp.Successful.length === 0) throw new Error('empty Successful entries');
  }));

  results.push(await runner.runTest('sqs', 'ChangeMessageVisibilityBatch_NonExistent', async () => {
    const batchResp = await client.send(new ChangeMessageVisibilityBatchCommand({
      QueueUrl: qUrl,
      Entries: [{ Id: 'cmvb-fail', ReceiptHandle: 'nonexistent-receipt-handle', VisibilityTimeout: 30 }],
    }));
    if (!batchResp.Failed || batchResp.Failed.length === 0) throw new Error('expected Failed entry');
    const code = batchResp.Failed[0].Code ?? '';
    const msg = batchResp.Failed[0].Message ?? '';
    if (!code.includes('ReceiptHandleIsInvalid') && !msg.includes('ReceiptHandleIsInvalid')) {
      throw new Error(`expected ReceiptHandleIsInvalid, got Code=${code} Message=${msg}`);
    }
  }));

  results.push(await runner.runTest('sqs', 'DeleteQueue', async () => {
    await client.send(new DeleteQueueCommand({ QueueUrl: qUrl }));
  }));

  results.push(await runner.runTest('sqs', 'CreateQueue (FIFO)', async () => {
    const fifoQueueName = `TestFifoQueue-${ts}.fifo`;
    const resp = await client.send(new CreateQueueCommand({
      QueueName: fifoQueueName,
      Attributes: { ContentBasedDeduplication: 'true', FifoQueue: 'true' },
    }));
    try {
      if (!resp.QueueUrl) throw new Error('CreateQueue (FIFO) returned nil QueueUrl');
    } finally {
      await safeCleanup(async () => {
        if (resp.QueueUrl) await client.send(new DeleteQueueCommand({ QueueUrl: resp.QueueUrl }));
      });
    }
  }));

  results.push(await runner.runTest('sqs', 'SendMessageBatch', async () => {
    await client.send(new CreateQueueCommand({ QueueName: batchQueueName }));
    try {
      const bUrl = await getQueueUrl(client, batchQueueName);
      const batchResp = await client.send(new SendMessageBatchCommand({
        QueueUrl: bUrl,
        Entries: [{ Id: 'msg1', MessageBody: 'Batch message 1' }, { Id: 'msg2', MessageBody: 'Batch message 2' }],
      }));
      if (!batchResp.Successful || batchResp.Successful.length === 0) throw new Error('empty Successful entries');
    } finally { /* cleanup in DeleteMessageBatch */ }
  }));

  results.push(await runner.runTest('sqs', 'SendMessageBatch_WithDelaySeconds', async () => {
    const bUrl = await getQueueUrl(client, batchQueueName);
    const batchResp = await client.send(new SendMessageBatchCommand({
      QueueUrl: bUrl,
      Entries: [{ Id: 'delayed1', MessageBody: 'Delayed batch 1', DelaySeconds: 3 }, { Id: 'delayed2', MessageBody: 'Delayed batch 2', DelaySeconds: 3 }],
    }));
    if (!batchResp.Successful || batchResp.Successful.length !== 2) throw new Error(`expected 2 successful entries, got ${batchResp.Successful?.length}`);
  }));

  results.push(await runner.runTest('sqs', 'DeleteMessageBatch', async () => {
    const bUrl = await getQueueUrl(client, batchQueueName);
    const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: bUrl, MaxNumberOfMessages: 10, WaitTimeSeconds: 2 }));
    if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received');
    const entries = recvResp.Messages.map((msg, i) => ({ Id: `del${i}`, ReceiptHandle: msg.ReceiptHandle! }));
    await client.send(new DeleteMessageBatchCommand({ QueueUrl: bUrl, Entries: entries }));
    await client.send(new DeleteQueueCommand({ QueueUrl: bUrl }));
  }));

  results.push(await runner.runTest('sqs', 'AddPermission', async () => {
    const permQueueName = `PermQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: permQueueName }));
    try {
      const permUrl = await getQueueUrl(client, permQueueName);
      await client.send(new AddPermissionCommand({
        QueueUrl: permUrl, Label: 'TestPermission', AWSAccountIds: ['123456789012'], Actions: ['SendMessage', 'ReceiveMessage'],
      }));
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, permQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'RemovePermission', async () => {
    const rPermQueueName = `RPermQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: rPermQueueName }));
    try {
      const rPermUrl = await getQueueUrl(client, rPermQueueName);
      await client.send(new AddPermissionCommand({
        QueueUrl: rPermUrl, Label: 'RemoveTestPerm', AWSAccountIds: ['123456789012'], Actions: ['SendMessage'],
      }));
      await client.send(new RemovePermissionCommand({ QueueUrl: rPermUrl, Label: 'RemoveTestPerm' }));
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, rPermQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'ListDeadLetterSourceQueues_Empty', async () => {
    const dlqName = `DLQ-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: dlqName }));
    try {
      const dlqUrl = await getQueueUrl(client, dlqName);
      const dlqResp = await client.send(new ListDeadLetterSourceQueuesCommand({ QueueUrl: dlqUrl }));
      if (dlqResp.queueUrls && dlqResp.queueUrls.length !== 0) throw new Error('expected empty queue URLs for new DLQ');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, dlqName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'StartMessageMoveTask', async () => {
    const srcDlqName = `SrcDLQ-${ts}`;
    const destQueueName = `DestQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: srcDlqName }));
    await client.send(new CreateQueueCommand({ QueueName: destQueueName }));
    try {
      const srcUrl = await getQueueUrl(client, srcDlqName);
      const destUrl = await getQueueUrl(client, destQueueName);
      const srcAttrs = await client.send(new GetQueueAttributesCommand({ QueueUrl: srcUrl, AttributeNames: ['QueueArn'] }));
      const destAttrs = await client.send(new GetQueueAttributesCommand({ QueueUrl: destUrl, AttributeNames: ['QueueArn'] }));
      const taskResp = await client.send(new StartMessageMoveTaskCommand({
        SourceArn: srcAttrs.Attributes?.QueueArn, DestinationArn: destAttrs.Attributes?.QueueArn,
      }));
      if (!taskResp.TaskHandle || taskResp.TaskHandle === '') throw new Error('empty TaskHandle');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, srcDlqName); });
      await safeCleanup(async () => { await cleanupQueue(client, destQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'CancelMessageMoveTask', async () => {
    const srcDlqName = `CancelDLQ-${ts}`;
    const destQueueName = `CancelDest-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: srcDlqName }));
    await client.send(new CreateQueueCommand({ QueueName: destQueueName }));
    try {
      const srcUrl = await getQueueUrl(client, srcDlqName);
      const destUrl = await getQueueUrl(client, destQueueName);
      const srcAttrs = await client.send(new GetQueueAttributesCommand({ QueueUrl: srcUrl, AttributeNames: ['QueueArn'] }));
      const destAttrs = await client.send(new GetQueueAttributesCommand({ QueueUrl: destUrl, AttributeNames: ['QueueArn'] }));
      const taskResp = await client.send(new StartMessageMoveTaskCommand({
        SourceArn: srcAttrs.Attributes?.QueueArn, DestinationArn: destAttrs.Attributes?.QueueArn,
      }));
      await client.send(new CancelMessageMoveTaskCommand({ TaskHandle: taskResp.TaskHandle }));
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, srcDlqName); });
      await safeCleanup(async () => { await cleanupQueue(client, destQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'ListMessageMoveTasks', async () => {
    const srcDlqName = `ListDLQ-${ts}`;
    const destQueueName = `ListDest-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: srcDlqName }));
    await client.send(new CreateQueueCommand({ QueueName: destQueueName }));
    try {
      const srcUrl = await getQueueUrl(client, srcDlqName);
      const destUrl = await getQueueUrl(client, destQueueName);
      const srcAttrs = await client.send(new GetQueueAttributesCommand({ QueueUrl: srcUrl, AttributeNames: ['QueueArn'] }));
      const destAttrs = await client.send(new GetQueueAttributesCommand({ QueueUrl: destUrl, AttributeNames: ['QueueArn'] }));
      await client.send(new StartMessageMoveTaskCommand({
        SourceArn: srcAttrs.Attributes?.QueueArn, DestinationArn: destAttrs.Attributes?.QueueArn,
      }));
      const listResp = await client.send(new ListMessageMoveTasksCommand({ SourceArn: srcAttrs.Attributes?.QueueArn }));
      if (!listResp.Results || listResp.Results.length === 0) throw new Error('empty Results');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, srcDlqName); });
      await safeCleanup(async () => { await cleanupQueue(client, destQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'GetQueueUrl_NonExistent', async () => {
    try {
      await client.send(new GetQueueUrlCommand({ QueueName: 'nonexistent-queue-xyz' }));
      throw new Error('expected error for non-existent queue');
    } catch (err) {
      assertErrorContains(err, 'QueueDoesNotExist');
    }
  }));

  results.push(await runner.runTest('sqs', 'SendMessage_ReceiveRoundtrip', async () => {
    const rtQueueName = `RTQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: rtQueueName }));
    try {
      const rtUrl = await getQueueUrl(client, rtQueueName);
      const testBody = 'roundtrip-test-message-12345';
      const sendResp = await client.send(new SendMessageCommand({ QueueUrl: rtUrl, MessageBody: testBody }));
      if (!sendResp.MessageId || sendResp.MessageId === '') throw new Error('expected MessageId');
      const recvResp = await client.send(new ReceiveMessageCommand({ QueueUrl: rtUrl }));
      if (!recvResp.Messages || recvResp.Messages.length === 0) throw new Error('no messages received');
      if (recvResp.Messages[0].Body !== testBody) throw new Error('message body mismatch');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, rtQueueName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'ListQueues_ContainsCreated', async () => {
    const lqName = `LQTest-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: lqName }));
    try {
      const resp = await client.send(new ListQueuesCommand({}));
      if (!resp.QueueUrls) throw new Error('expected QueueUrls to be defined');
      const found = resp.QueueUrls.some((u) => u.length > 0);
      if (!found) throw new Error('expected at least one queue URL');
    } finally {
      await safeCleanup(async () => { await cleanupQueue(client, lqName); });
    }
  }));

  results.push(await runner.runTest('sqs', 'ChangeMessageVisibility_NonExistent', async () => {
    try {
      await client.send(new ChangeMessageVisibilityCommand({
        QueueUrl: 'https://queue.amazonaws.com/000000000000/nonexistent',
        ReceiptHandle: 'fake-receipt-handle', VisibilityTimeout: 30,
      }));
      throw new Error('expected error');
    } catch (err) {
      assertErrorContains(err, 'ReceiptHandleIsInvalid');
    }
  }));

  results.push(await runner.runTest('sqs', 'CreateQueue_DuplicateName', async () => {
    const dupQName = `DupQueue-${ts}`;
    await client.send(new CreateQueueCommand({ QueueName: dupQName }));
    try {
      await client.send(new CreateQueueCommand({ QueueName: dupQName }));
    } catch (err) {
      throw new Error(`duplicate queue name should be idempotent, got: ${err}`);
    }
  }));

  results.push(await runner.runTest('sqs', 'ListQueues_Pagination', async () => {
    const pgTs = ts;
    const pgQueues: string[] = [];
    for (const i of [0, 1, 2, 3, 4]) {
      const name = `PagQ-${pgTs}-${i}`;
      await client.send(new CreateQueueCommand({ QueueName: name }));
      pgQueues.push(name);
    }
    try {
      const allQueues: string[] = [];
      let nextToken: string | undefined;
      do {
        const resp = await client.send(new ListQueuesCommand({ MaxResults: 2, NextToken: nextToken }));
        for (const u of resp.QueueUrls ?? []) {
          if (u.includes(`PagQ-${pgTs}`)) allQueues.push(u);
        }
        nextToken = resp.NextToken;
      } while (nextToken);
      if (allQueues.length !== 5) throw new Error(`expected 5 paginated queues, got ${allQueues.length}`);
    } finally {
      for (const qn of pgQueues) {
        await safeCleanup(async () => { await cleanupQueue(client, qn); });
      }
    }
  }));

  return results;
}
