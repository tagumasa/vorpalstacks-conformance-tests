import {
  DeleteInstanceProfileCommand,
  DeleteLoginProfileCommand,
  DeleteAccessKeyCommand,
  DeleteUserCommand,
  DeleteGroupCommand,
  DeleteRoleCommand,
  DeletePolicyCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';

export async function runDeleteTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'DeleteInstanceProfile', async () => {
    await client.send(new DeleteInstanceProfileCommand({ InstanceProfileName: ctx.profileName }));
  }));

  results.push(await runner.runTest('iam', 'DeleteLoginProfile', async () => {
    await client.send(new DeleteLoginProfileCommand({ UserName: ctx.userName }));
  }));

  results.push(await runner.runTest('iam', 'DeleteAccessKey', async () => {
    await client.send(new DeleteAccessKeyCommand({ UserName: ctx.userName, AccessKeyId: ctx.accessKeyId }));
  }));

  results.push(await runner.runTest('iam', 'DeleteUser', async () => {
    await client.send(new DeleteUserCommand({ UserName: ctx.userName }));
  }));

  results.push(await runner.runTest('iam', 'DeleteGroup', async () => {
    await client.send(new DeleteGroupCommand({ GroupName: ctx.groupName }));
  }));

  results.push(await runner.runTest('iam', 'DeleteRole', async () => {
    await client.send(new DeleteRoleCommand({ RoleName: ctx.roleName }));
  }));

  results.push(await runner.runTest('iam', 'DeletePolicy', async () => {
    await client.send(new DeletePolicyCommand({ PolicyArn: ctx.policyArn }));
  }));

  return results;
}
