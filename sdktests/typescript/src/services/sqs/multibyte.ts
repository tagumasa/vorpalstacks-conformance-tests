import {
  SQSClient,
  CreateQueueCommand,
  SendMessageCommand,
  ReceiveMessageCommand,
  DeleteMessageCommand,
  DeleteQueueCommand,
} from '@aws-sdk/client-sqs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: SQSClient,
  _qUrl: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sqs', 'MultiByteMessage', async () => {
    const queueName = makeUniqueName('MBQueue');
    const createResp = await client.send(new CreateQueueCommand({ QueueName: queueName }));
    const qUrl = createResp.QueueUrl!;
    try {
      const messages = [
        '日本語テストメッセージ',
        '简体中文测试消息',
        '繁體中文測試訊息',
      ];
      for (const body of messages) {
        await client.send(new SendMessageCommand({ QueueUrl: qUrl, MessageBody: body }));
      }
      const received = new Set<string>();
      for (let i = 0; i < 3; i++) {
        const resp = await client.send(new ReceiveMessageCommand({
          QueueUrl: qUrl, MaxNumberOfMessages: 1, WaitTimeSeconds: 2,
        }));
        for (const msg of resp.Messages ?? []) {
          received.add(msg.Body!);
          await client.send(new DeleteMessageCommand({ QueueUrl: qUrl, ReceiptHandle: msg.ReceiptHandle! }));
        }
      }
      for (const body of messages) {
        if (!received.has(body)) {
          throw new Error(`message not received: ${JSON.stringify(body)}`);
        }
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteQueueCommand({ QueueUrl: qUrl })));
    }
  }));

  return results;
}
