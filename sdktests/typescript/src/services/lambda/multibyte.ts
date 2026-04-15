import { LambdaClient, CreateFunctionCommand, InvokeCommand, DeleteFunctionCommand } from '@aws-sdk/client-lambda';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, createIAMRole } from '../../helpers.js';
import { lambdaTrustPolicy } from './context.js';

export async function runMultibyteTests(
  _ctx: unknown,
  runner: TestRunner,
): Promise<TestResult[]> {
  const ctx = _ctx as { client: LambdaClient; iamClient: IAMClient; ts: string; functionCode: Uint8Array };
  const { client, iamClient, ts, functionCode } = ctx;
  const results: TestResult[] = [];

  results.push(await runner.runTest('lambda', 'MultiByteInvoke', async () => {
    const funcName = `MBFunc-${ts}`;
    const roleName = `MBRole-${ts}`;
    const roleArn = `arn:aws:iam::000000000000:role/${roleName}`;
    await createIAMRole(iamClient, roleName, lambdaTrustPolicy);
    try {
      await client.send(new CreateFunctionCommand({
        FunctionName: funcName,
        Runtime: 'nodejs22.x',
        Role: roleArn,
        Handler: 'index.handler',
        Code: { ZipFile: functionCode },
      }));
      try {
        const payloads = [
          { label: 'ja', data: { message: 'こんにちは世界！ラムダ実行テスト。' } },
          { label: 'zh', data: { message: '数据库连接成功！测试完成。' } },
          { label: 'tw', data: { message: '憑證驗證通過！繁體中文測試。' } },
        ];
        for (const { label, data } of payloads) {
          const payload = new TextEncoder().encode(JSON.stringify(data));
          const resp = await client.send(new InvokeCommand({
            FunctionName: funcName,
            Payload: payload,
            LogType: 'None',
          }));
          if (resp.StatusCode !== 200) {
            throw new Error(`invoke failed for ${label}: status ${resp.StatusCode}`);
          }
          if (resp.FunctionError) {
            throw new Error(`invoke error for ${label}: ${resp.FunctionError}`);
          }
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteFunctionCommand({ FunctionName: funcName })));
      }
    } finally {
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
    }
  }));

  return results;
}
