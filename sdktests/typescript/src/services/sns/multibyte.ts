import { SNSClient, CreateTopicCommand, PublishCommand, DeleteTopicCommand } from '@aws-sdk/client-sns';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: SNSClient,
  _topicArn: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('sns', 'MultiBytePublish', async () => {
    const topicName = makeUniqueName('MBTopic');
    const createResp = await client.send(new CreateTopicCommand({ Name: topicName }));
    const topicArn = createResp.TopicArn!;
    try {
      const messages = [
        '日本語テストメッセージ',
        '简体中文测试消息',
        '繁體中文測試訊息',
      ];
      for (const msg of messages) {
        const resp = await client.send(new PublishCommand({ TopicArn: topicArn, Message: msg }));
        if (!resp.MessageId) throw new Error('expected MessageId to be defined');
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteTopicCommand({ TopicArn: topicArn })));
    }
  }));

  return results;
}
