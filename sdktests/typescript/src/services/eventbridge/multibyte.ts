import { EventBridgeClient, PutEventsCommand } from '@aws-sdk/client-eventbridge';
import type { ServiceContext, TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: EventBridgeClient,
  ctx: ServiceContext,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('eventbridge', 'MultiByteEvent', async () => {
    const events = [
      { detail: JSON.stringify({ message: '日本語イベントテスト：テスト完了' }) },
      { detail: JSON.stringify({ message: '简体中文事件测试：测试完成' }) },
      { detail: JSON.stringify({ message: '繁體中文事件測試：測試完成' }) },
    ];
    const resp = await client.send(new PutEventsCommand({
      Entries: events.map((e) => ({
        Source: 'multibyte-test',
        DetailType: 'MultiByteTest',
        Detail: e.detail,
      })),
    }));
    if (!resp.Entries || resp.Entries.length !== 3) {
      throw new Error(`expected 3 entries, got ${resp.Entries?.length}`);
    }
    for (const entry of resp.Entries) {
      if (entry.ErrorCode) {
        throw new Error(`putEvents error: ${entry.ErrorCode} - ${entry.ErrorMessage}`);
      }
    }
  }));

  return results;
}
