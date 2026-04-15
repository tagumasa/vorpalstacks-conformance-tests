import {
  CreateGroupCommand,
  GetGroupCommand,
  ListGroupsCommand,
  UpdateGroupCommand,
  AddUserToGroupCommand,
  ListGroupsForUserCommand,
  RemoveUserFromGroupCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';

export async function runGroupTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'CreateGroup', async () => {
    const resp = await client.send(new CreateGroupCommand({ GroupName: ctx.groupName }));
    if (!resp.Group) throw new Error('group to be defined');
  }));

  results.push(await runner.runTest('iam', 'GetGroup', async () => {
    const resp = await client.send(new GetGroupCommand({ GroupName: ctx.groupName }));
    if (!resp.Group) throw new Error('group to be defined');
  }));

  results.push(await runner.runTest('iam', 'ListGroups', async () => {
    const resp = await client.send(new ListGroupsCommand({}));
    if (!resp.Groups) throw new Error('groups list to be defined');
  }));

  results.push(await runner.runTest('iam', 'UpdateGroup', async () => {
    await client.send(new UpdateGroupCommand({
      GroupName: ctx.groupName,
      NewGroupName: ctx.groupName + '-renamed',
    }));
    ctx.groupName = ctx.groupName + '-renamed';
  }));

  results.push(await runner.runTest('iam', 'AddUserToGroup', async () => {
    await client.send(new AddUserToGroupCommand({ GroupName: ctx.groupName, UserName: ctx.userName }));
  }));

  results.push(await runner.runTest('iam', 'ListGroupsForUser', async () => {
    const resp = await client.send(new ListGroupsForUserCommand({ UserName: ctx.userName }));
    if (!resp.Groups) throw new Error('groups list to be defined');
    const found = resp.Groups.some((g) => g.GroupName === ctx.groupName);
    if (!found) throw new Error(`group ${ctx.groupName} not found in ListGroupsForUser response`);
  }));

  results.push(await runner.runTest('iam', 'RemoveUserFromGroup', async () => {
    await client.send(new RemoveUserFromGroupCommand({ GroupName: ctx.groupName, UserName: ctx.userName }));
  }));

  return results;
}
