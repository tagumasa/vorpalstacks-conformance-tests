import {
  SFNClient,
  CreateStateMachineCommand,
  DeleteStateMachineVersionCommand,
  DeleteStateMachineCommand,
} from '@aws-sdk/client-sfn';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import type { ServiceRegistration, ServiceContext, TestRunner } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import { runCRUDTests } from './crud.js';
import { runErrorTests } from './error-tests.js';
import { runDefinitionTests } from './definition-tests.js';
import { runVersionAndAliasTests } from './version-alias.js';
import { runPaginationTests } from './pagination.js';
import { runMultibyteTests } from './multibyte.js';

const TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'states.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

const PASS_DEF = JSON.stringify({
  Comment: 'A Hello World example',
  StartAt: 'HelloWorld',
  States: { HelloWorld: { Type: 'Pass', End: true } },
});

export function registerStepFunctions(): ServiceRegistration {
  return {
    name: 'stepfunctions',
    category: 'sdk',
    run: async (runner: TestRunner, ctx: ServiceContext) => {
      const client = new SFNClient({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials });
      const iamClient = new IAMClient({ endpoint: ctx.endpoint, region: ctx.region, credentials: ctx.credentials });
      const results: Awaited<ReturnType<typeof runner.runTest>>[] = [];

      const smName = makeUniqueName('TestStateMachine');
      const activityName = makeUniqueName('TestActivity');
      const roleName = makeUniqueName('TestSfnRole');
      const roleARN = `arn:aws:iam::000000000000:role/${roleName}`;

      try {
        await iamClient.send(new CreateRoleCommand({
          RoleName: roleName,
          AssumeRolePolicyDocument: TRUST_POLICY,
        }));
      } catch (e) {
        return [await runner.runTest('stepfunctions', 'Setup', async () => {
          throw new Error(`Failed to create IAM role: ${e instanceof Error ? e.message : String(e)}`);
        })];
      }

      const state = { stateMachineARN: '', executionARN: '', activityARN: '' };
      const verify = { name: '', arn: '' };

      try {
        await runCRUDTests(runner, client, smName, roleARN, PASS_DEF, activityName, results, state);
        await runErrorTests(runner, client, iamClient, ctx, results);
        await runDefinitionTests(runner, client, iamClient, ctx, results, state.executionARN, verify);
        const { secondVersionARN } = await runVersionAndAliasTests(runner, client, ctx, results, verify);
        await runPaginationTests(runner, client, roleARN, results);
        await runMultibyteTests(runner, client, iamClient, state.stateMachineARN, results);

        await safeCleanup(async () => {
          if (secondVersionARN) {
            await client.send(new DeleteStateMachineVersionCommand({ stateMachineVersionArn: secondVersionARN }));
          }
        });
        await safeCleanup(async () => {
          if (verify.arn) {
            await client.send(new DeleteStateMachineCommand({ stateMachineArn: verify.arn }));
          }
        });
      } finally {
        await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
      }

      return results;
    },
  };
}
