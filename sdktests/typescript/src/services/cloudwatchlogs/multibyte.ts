import {
  CloudWatchLogsClient,
  CreateLogGroupCommand,
  CreateLogStreamCommand,
  PutLogEventsCommand,
  GetLogEventsCommand,
  DeleteLogStreamCommand,
  DeleteLogGroupCommand,
} from '@aws-sdk/client-cloudwatch-logs';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: CloudWatchLogsClient,
  _logGroupName: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('cloudwatchlogs', 'MultiByteLogEvent', async () => {
    const logGroupName = makeUniqueName('MBLogGroup');
    await client.send(new CreateLogGroupCommand({ logGroupName }));
    try {
      const streamName = `multibyte-stream-${Date.now()}`;
      await client.send(new CreateLogStreamCommand({ logGroupName, logStreamName: streamName }));
      try {
        const logEvents = [
          { message: '日本語ログメッセージ：テスト完了', timestamp: Date.now() - 2000 },
          { message: '简体中文日志消息：测试完成', timestamp: Date.now() - 1000 },
          { message: '繁體中文日誌訊息：測試完成', timestamp: Date.now() },
        ];
        await client.send(new PutLogEventsCommand({ logGroupName, logStreamName: streamName, logEvents }));
        const resp = await client.send(new GetLogEventsCommand({ logGroupName, logStreamName: streamName }));
        const messages = (resp.events ?? []).map((e) => e.message);
        for (const expected of logEvents) {
          if (!messages.includes(expected.message)) {
            throw new Error(`log event not found: ${JSON.stringify(expected.message)}`);
          }
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteLogStreamCommand({ logGroupName, logStreamName: streamName })));
      }
    } finally {
      await safeCleanup(() => client.send(new DeleteLogGroupCommand({ logGroupName })));
    }
  }));
}
