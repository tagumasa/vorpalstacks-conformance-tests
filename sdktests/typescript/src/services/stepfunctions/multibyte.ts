import {
  SFNClient,
  CreateStateMachineCommand,
  StartExecutionCommand,
  DescribeExecutionCommand,
  DeleteStateMachineCommand,
} from '@aws-sdk/client-sfn';
import { IAMClient } from '@aws-sdk/client-iam';
import { createIAMRole, deleteIAMRole, safeCleanup } from '../../helpers.js';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName } from '../../helpers.js';

const TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'states.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

const PASS_DEF = JSON.stringify({
  Comment: 'MultiByte test',
  StartAt: 'Pass',
  States: { Pass: { Type: 'Pass', End: true } },
});

export async function runMultibyteTests(
  runner: TestRunner,
  client: SFNClient,
  iamClient: IAMClient,
  _stateMachineARN: string,
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('stepfunctions', 'MultiByteExecutionInput', async () => {
    const smName = makeUniqueName('MBSM');
    const roleName = makeUniqueName('MBSfnRole');
    const roleARN = `arn:aws:iam::000000000000:role/${roleName}`;

    await createIAMRole(iamClient, roleName, TRUST_POLICY);
    try {
      const createResp = await client.send(new CreateStateMachineCommand({
        name: smName,
        definition: PASS_DEF,
        roleArn: roleARN,
      }));
      const smArn = createResp.stateMachineArn!;
      try {
        const inputs = [
          { label: 'ja', data: { message: 'こんにちは世界！ステートマシン実行テスト。' } },
          { label: 'zh', data: { message: '数据库连接成功！状态机执行测试。' } },
          { label: 'tw', data: { message: '憑證驗證通過！狀態機執行測試。' } },
        ];
        for (const { label, data } of inputs) {
          const execResp = await client.send(new StartExecutionCommand({
            stateMachineArn: smArn,
            input: JSON.stringify(data),
          }));
          const descResp = await client.send(new DescribeExecutionCommand({
            executionArn: execResp.executionArn,
          }));
          if (!descResp.input) {
            throw new Error(`execution input is empty for ${label}`);
          }
          const parsed = JSON.parse(descResp.input);
          if (parsed.message !== data.message) {
            throw new Error(`input mismatch for ${label}: expected ${JSON.stringify(data.message)}, got ${JSON.stringify(parsed.message)}`);
          }
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteStateMachineCommand({ stateMachineArn: smArn })));
      }
    } finally {
      await deleteIAMRole(iamClient, roleName);
    }
  }));
}
