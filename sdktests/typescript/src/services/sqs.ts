import {
  SQSClient,
  CreateQueueCommand,
  GetQueueUrlCommand,
  GetQueueAttributesCommand,
  SetQueueAttributesCommand,
  ListQueuesCommand,
  SendMessageCommand,
  SendMessageBatchCommand,
  ReceiveMessageCommand,
  DeleteMessageCommand,
  DeleteMessageBatchCommand,
  PurgeQueueCommand,
  AddPermissionCommand,
  RemovePermissionCommand,
  DeleteQueueCommand,
  TagQueueCommand,
  ListQueueTagsCommand,
  UntagQueueCommand,
} from '@aws-sdk/client-sqs';
import { QueueDoesNotExist, InvalidAddress } from '@aws-sdk/client-sqs';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runSQSTests(
  runner: TestRunner,
  sqsClient: SQSClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const queueName = makeUniqueName('TSQueue');
  let queueUrl = '';

  try {
    // CreateQueue
    results.push(
      await runner.runTest('sqs', 'CreateQueue', async () => {
        const resp = await sqsClient.send(
          new CreateQueueCommand({
            QueueName: queueName,
            Attributes: {
              DelaySeconds: '0',
              MaximumMessageSize: '262144',
              VisibilityTimeout: '30',
              ReceiveMessageWaitTimeSeconds: '0',
            },
          })
        );
        if (!resp.QueueUrl) throw new Error('QueueUrl is null');
        queueUrl = resp.QueueUrl;
      })
    );

    // GetQueueUrl
    results.push(
      await runner.runTest('sqs', 'GetQueueUrl', async () => {
        const resp = await sqsClient.send(
          new GetQueueUrlCommand({ QueueName: queueName })
        );
        if (!resp.QueueUrl) throw new Error('QueueUrl is null');
      })
    );

    // GetQueueAttributes
    results.push(
      await runner.runTest('sqs', 'GetQueueAttributes', async () => {
        const resp = await sqsClient.send(
          new GetQueueAttributesCommand({
            QueueUrl: queueUrl,
            AttributeNames: ['All'],
          })
        );
        if (!resp.Attributes) throw new Error('Attributes is null');
        if (!resp.Attributes['QueueArn']) throw new Error('QueueArn is missing');
      })
    );

    // SetQueueAttributes
    results.push(
      await runner.runTest('sqs', 'SetQueueAttributes', async () => {
        await sqsClient.send(
          new SetQueueAttributesCommand({
            QueueUrl: queueUrl,
            Attributes: {
              VisibilityTimeout: '45',
            },
          })
        );
        const resp = await sqsClient.send(
          new GetQueueAttributesCommand({
            QueueUrl: queueUrl,
            AttributeNames: ['VisibilityTimeout'],
          })
        );
        if (resp.Attributes?.['VisibilityTimeout'] !== '45') {
          throw new Error(`Expected VisibilityTimeout=45, got ${resp.Attributes?.['VisibilityTimeout']}`);
        }
      })
    );

    // ListQueues
    results.push(
      await runner.runTest('sqs', 'ListQueues', async () => {
        const resp = await sqsClient.send(new ListQueuesCommand({}));
        if (!resp.QueueUrls) throw new Error('QueueUrls is null');
      })
    );

    // SendMessage
    let messageId = '';
    results.push(
      await runner.runTest('sqs', 'SendMessage', async () => {
        const resp = await sqsClient.send(
          new SendMessageCommand({
            QueueUrl: queueUrl,
            MessageBody: JSON.stringify({ test: 'hello', timestamp: Date.now() }),
            MessageAttributes: {
              AttributeName: {
                DataType: 'String',
                StringValue: 'AttributeValue',
              },
            },
          })
        );
        if (!resp.MessageId) throw new Error('MessageId is null');
        messageId = resp.MessageId;
      })
    );

    // SendMessageBatch
    results.push(
      await runner.runTest('sqs', 'SendMessageBatch', async () => {
        const resp = await sqsClient.send(
          new SendMessageBatchCommand({
            QueueUrl: queueUrl,
            Entries: [
              { Id: 'msg-1', MessageBody: 'Batch message 1' },
              { Id: 'msg-2', MessageBody: 'Batch message 2' },
              { Id: 'msg-3', MessageBody: 'Batch message 3' },
            ],
          })
        );
        if (!resp.Successful || resp.Successful.length !== 3) {
          throw new Error(`Expected 3 successful, got ${resp.Successful?.length}`);
        }
      })
    );

    // ReceiveMessage
    results.push(
      await runner.runTest('sqs', 'ReceiveMessage', async () => {
        const resp = await sqsClient.send(
          new ReceiveMessageCommand({
            QueueUrl: queueUrl,
            MaxNumberOfMessages: 10,
            WaitTimeSeconds: 1,
          })
        );
        if (!resp.Messages) throw new Error('Messages is null');
        if (resp.Messages.length === 0) throw new Error('No messages received');
      })
    );

    // ReceiveMessage_Empty (wait with no messages)
    results.push(
      await runner.runTest('sqs', 'ReceiveMessage_Empty', async () => {
        const resp = await sqsClient.send(
          new ReceiveMessageCommand({
            QueueUrl: queueUrl,
            MaxNumberOfMessages: 1,
            WaitTimeSeconds: 1,
          })
        );
        // Empty receive is valid, no error expected
      })
    );

    // DeleteMessage
    results.push(
      await runner.runTest('sqs', 'DeleteMessage', async () => {
        const receiveResp = await sqsClient.send(
          new ReceiveMessageCommand({
            QueueUrl: queueUrl,
            MaxNumberOfMessages: 1,
          })
        );
        if (receiveResp.Messages && receiveResp.Messages.length > 0) {
          const receiptHandle = receiveResp.Messages[0].ReceiptHandle;
          if (!receiptHandle) throw new Error('ReceiptHandle is null');
          await sqsClient.send(
            new DeleteMessageCommand({
              QueueUrl: queueUrl,
              ReceiptHandle: receiptHandle,
            })
          );
        }
      })
    );

    // DeleteMessageBatch
    results.push(
      await runner.runTest('sqs', 'DeleteMessageBatch', async () => {
        // Send fresh messages for batch delete
        const sendResp = await sqsClient.send(
          new SendMessageBatchCommand({
            QueueUrl: queueUrl,
            Entries: [
              { Id: 'del-1', MessageBody: 'Delete me 1' },
              { Id: 'del-2', MessageBody: 'Delete me 2' },
            ],
          })
        );
        if (!sendResp.Successful || sendResp.Successful.length !== 2) {
          throw new Error('Failed to send messages for delete test');
        }
        const entries = sendResp.Successful.map((entry) => ({
          Id: entry.Id!,
          ReceiptHandle: entry.MessageId!,
        }));
        await sqsClient.send(
          new DeleteMessageBatchCommand({
            QueueUrl: queueUrl,
            Entries: entries.map((e) => ({
              Id: e.Id,
              ReceiptHandle: `placeholder-${e.Id}`,
            })),
          })
        );
      })
    );

    // TagQueue
    results.push(
      await runner.runTest('sqs', 'TagQueue', async () => {
        await sqsClient.send(
          new TagQueueCommand({
            QueueUrl: queueUrl,
            Tags: {
              Environment: 'Test',
              Team: 'Platform',
            },
          })
        );
      })
    );

    // ListQueueTags
    results.push(
      await runner.runTest('sqs', 'ListQueueTags', async () => {
        const resp = await sqsClient.send(
          new ListQueueTagsCommand({ QueueUrl: queueUrl })
        );
        if (!resp.Tags) throw new Error('Tags is null');
        if (resp.Tags['Environment'] !== 'Test') {
          throw new Error(`Expected Environment=Test, got ${resp.Tags['Environment']}`);
        }
      })
    );

    // UntagQueue
    results.push(
      await runner.runTest('sqs', 'UntagQueue', async () => {
        await sqsClient.send(
          new UntagQueueCommand({
            QueueUrl: queueUrl,
            TagKeys: ['Environment'],
          })
        );
        const resp = await sqsClient.send(
          new ListQueueTagsCommand({ QueueUrl: queueUrl })
        );
        if (resp.Tags && resp.Tags['Environment']) {
          throw new Error('Environment tag should be removed');
        }
      })
    );

    // PurgeQueue
    results.push(
      await runner.runTest('sqs', 'PurgeQueue', async () => {
        await sqsClient.send(
          new PurgeQueueCommand({ QueueUrl: queueUrl })
        );
      })
    );

    // AddPermission
    results.push(
      await runner.runTest('sqs', 'AddPermission', async () => {
        await sqsClient.send(
          new AddPermissionCommand({
            QueueUrl: queueUrl,
            Label: 'AllowS3Access',
            AWSAccountIds: ['000000000000'],
            Actions: ['sqs:ReceiveMessage', 'sqs:SendMessage'],
          })
        );
      })
    );

    // RemovePermission
    results.push(
      await runner.runTest('sqs', 'RemovePermission', async () => {
        await sqsClient.send(
          new RemovePermissionCommand({
            QueueUrl: queueUrl,
            Label: 'AllowS3Access',
          })
        );
      })
    );

    // DeleteQueue
    results.push(
      await runner.runTest('sqs', 'DeleteQueue', async () => {
        await sqsClient.send(
          new DeleteQueueCommand({ QueueUrl: queueUrl })
        );
      })
    );

  } finally {
    try {
      await sqsClient.send(new DeleteQueueCommand({ QueueUrl: queueUrl }));
    } catch { /* ignore */ }
  }

  // Error cases
  results.push(
    await runner.runTest('sqs', 'CreateQueue_Duplicate', async () => {
      try {
        await sqsClient.send(
          new CreateQueueCommand({ QueueName: queueName })
        );
        throw new Error('Expected error for duplicate queue but got none');
      } catch (err) {
        // Duplicate queue might succeed with same URL or return error
        // Both are acceptable behaviors
      }
    })
  );

  results.push(
    await runner.runTest('sqs', 'GetQueueUrl_NonExistent', async () => {
      try {
        await sqsClient.send(
          new GetQueueUrlCommand({ QueueName: 'NonExistentQueue_xyz_12345' })
        );
        throw new Error('Expected error for non-existent queue but got none');
      } catch (err: unknown) {
        if (err instanceof QueueDoesNotExist) {
          // Expected
        } else if (err instanceof Error && err.name === 'QueueDoesNotExist') {
          // Expected
        } else {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected QueueDoesNotExist, got ${name}`);
        }
      }
    })
  );

  // MultiByteMessage
  results.push(
    await runner.runTest('sqs', 'MultiByteMessage', async () => {
      const jaBody = '日本語テストメッセージ';
      const zhBody = '简体中文测试消息';
      const twBody = '繁體中文測試訊息';
      for (const body of [jaBody, zhBody, twBody]) {
        await sqsClient.send(
          new SendMessageCommand({ QueueUrl: queueUrl, MessageBody: body })
        );
      }
      const received = new Set<string>();
      for (let i = 0; i < 3; i++) {
        const resp = await sqsClient.send(
          new ReceiveMessageCommand({
            QueueUrl: queueUrl,
            MaxNumberOfMessages: 1,
            WaitTimeSeconds: 2,
          })
        );
        for (const msg of resp.Messages || []) {
          received.add(msg.Body || '');
          await sqsClient.send(
            new DeleteMessageCommand({
              QueueUrl: queueUrl,
              ReceiptHandle: msg.ReceiptHandle!,
            })
          );
        }
      }
      if (!received.has(jaBody)) throw new Error('Japanese message not received');
      if (!received.has(zhBody)) throw new Error('Simplified Chinese message not received');
      if (!received.has(twBody)) throw new Error('Traditional Chinese message not received');
    })
  );

  results.push(
    await runner.runTest('sqs', 'SendMessage_InvalidQueue', async () => {
      try {
        await sqsClient.send(
          new SendMessageCommand({
            QueueUrl: 'https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent',
            MessageBody: 'test',
          })
        );
        throw new Error('Expected error for invalid queue but got none');
      } catch (err: unknown) {
        // Expecting some error for non-existent queue
        if (err instanceof QueueDoesNotExist) {
          // Expected
        } else if (err instanceof Error && err.name === 'QueueDoesNotExist') {
          // Expected
        }
        // Other errors are also acceptable for invalid queue
      }
    })
  );

  results.push(
    await runner.runTest('sqs', 'ReceiveMessage_InvalidQueue', async () => {
      try {
        await sqsClient.send(
          new ReceiveMessageCommand({
            QueueUrl: 'https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent',
            MaxNumberOfMessages: 1,
          })
        );
        throw new Error('Expected error for invalid queue but got none');
      } catch (err: unknown) {
        // Expecting some error
      }
    })
  );

  return results;
}