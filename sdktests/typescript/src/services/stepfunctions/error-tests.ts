import {
  SFNClient,
  DescribeStateMachineCommand,
  DeleteStateMachineCommand,
  DescribeExecutionCommand,
  DescribeActivityCommand,
  CreateStateMachineCommand,
} from '@aws-sdk/client-sfn';
import { IAMClient, CreateRoleCommand, DeleteRoleCommand } from '@aws-sdk/client-iam';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertErrorContains, safeCleanup } from '../../helpers.js';

const TRUST_POLICY = JSON.stringify({
  Version: '2012-10-17',
  Statement: [{ Effect: 'Allow', Principal: { Service: 'states.amazonaws.com' }, Action: 'sts:AssumeRole' }],
});

export async function runErrorTests(
  runner: TestRunner,
  client: SFNClient,
  iamClient: IAMClient,
  ctx: { region: string },
  results: TestResult[],
): Promise<void> {
  results.push(await runner.runTest('stepfunctions', 'DescribeStateMachine_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DescribeStateMachineCommand({
        stateMachineArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:nonexistent-fake-arn',
      }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'StateMachineDoesNotExist');
  }));

  results.push(await runner.runTest('stepfunctions', 'DeleteStateMachine_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DeleteStateMachineCommand({
        stateMachineArn: 'arn:aws:states:us-east-1:000000000000:stateMachine:nonexistent-fake-arn',
      }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'StateMachineDoesNotExist');
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeExecution_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DescribeExecutionCommand({
        executionArn: 'arn:aws:states:us-east-1:000000000000:execution:nonexistent:fake-exec',
      }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'ExecutionDoesNotExist');
  }));

  results.push(await runner.runTest('stepfunctions', 'DescribeActivity_NonExistent', async () => {
    let err: unknown;
    try {
      await client.send(new DescribeActivityCommand({
        activityArn: 'arn:aws:states:us-east-1:000000000000:activity:nonexistent-fake-arn',
      }));
    } catch (e) {
      err = e;
    }
    assertErrorContains(err, 'ActivityDoesNotExist');
  }));

  results.push(await runner.runTest('stepfunctions', 'CreateStateMachine_InvalidDefinition', async () => {
    const smName = makeUniqueName('InvalidSM');
    const roleName = makeUniqueName('InvalidRole');
    try {
      await iamClient.send(new CreateRoleCommand({
        RoleName: roleName,
        AssumeRolePolicyDocument: TRUST_POLICY,
      }));
      try {
        await client.send(new CreateStateMachineCommand({
          name: smName,
          definition: 'not valid json {{{',
          roleArn: `arn:aws:iam::000000000000:role/${roleName}`,
        }));
      } catch (e) {
        await safeCleanup(() => client.send(new DeleteStateMachineCommand({
          stateMachineArn: `arn:aws:states:${ctx.region}:000000000000:stateMachine:${smName}`,
        })));
        throw new Error(`server rejected invalid definition: ${e instanceof Error ? e.message : String(e)}`);
      }
    } finally {
      await safeCleanup(() => iamClient.send(new DeleteRoleCommand({ RoleName: roleName })));
    }
  }));
}
