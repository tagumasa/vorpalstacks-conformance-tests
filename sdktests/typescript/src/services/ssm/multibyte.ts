import { SSMClient, PutParameterCommand, GetParameterCommand, DeleteParameterCommand } from '@aws-sdk/client-ssm';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';

export async function runMultibyteTests(
  runner: TestRunner,
  client: SSMClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest('ssm', 'MultiByteParameter', async () => {
    const cases: Array<{ label: string; value: string }> = [
      { label: 'ja', value: '日本語テストパラメータ' },
      { label: 'zh', value: '简体中文测试参数' },
      { label: 'tw', value: '繁體中文測試參數' },
    ];
    for (const { label, value } of cases) {
      const name = `/test/multibyte-${label}-${Date.now()}`;
      await client.send(new PutParameterCommand({ Name: name, Value: value, Type: 'String' }));
      try {
        const resp = await client.send(new GetParameterCommand({ Name: name }));
        if (resp.Parameter?.Value !== value) {
          throw new Error(`Mismatch for ${label}: expected ${JSON.stringify(value)}, got ${JSON.stringify(resp.Parameter?.Value)}`);
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteParameterCommand({ Name: name })));
      }
    }
  }));

  return results;
}
