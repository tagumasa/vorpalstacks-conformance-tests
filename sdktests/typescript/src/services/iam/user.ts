import {
  CreateUserCommand,
  GetUserCommand,
  ListUsersCommand,
  CreateAccessKeyCommand,
  ListAccessKeysCommand,
  CreateLoginProfileCommand,
  GetLoginProfileCommand,
  UpdateUserCommand,
  TagUserCommand,
  ListUserTagsCommand,
  UntagUserCommand,
  UpdateAccessKeyCommand,
  GetAccessKeyLastUsedCommand,
  UpdateLoginProfileCommand,
  DeleteUserCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runUserTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'CreateUser', async () => {
    const resp = await client.send(new CreateUserCommand({ UserName: ctx.userName }));
    if (!resp.User) throw new Error('user to be defined');
  }));

  results.push(await runner.runTest('iam', 'GetUser', async () => {
    const resp = await client.send(new GetUserCommand({ UserName: ctx.userName }));
    if (!resp.User) throw new Error('user to be defined');
  }));

  results.push(await runner.runTest('iam', 'ListUsers', async () => {
    const resp = await client.send(new ListUsersCommand({}));
    if (!resp.Users) throw new Error('users list to be defined');
  }));

  results.push(await runner.runTest('iam', 'CreateAccessKey', async () => {
    const resp = await client.send(new CreateAccessKeyCommand({ UserName: ctx.userName }));
    if (!resp.AccessKey) throw new Error('access key to be defined');
    if (!resp.AccessKey.AccessKeyId) throw new Error('access key id to be defined');
    ctx.accessKeyId = resp.AccessKey.AccessKeyId;
  }));

  results.push(await runner.runTest('iam', 'ListAccessKeys', async () => {
    const resp = await client.send(new ListAccessKeysCommand({ UserName: ctx.userName }));
    if (!resp.AccessKeyMetadata) throw new Error('access keys list to be defined');
  }));

  results.push(await runner.runTest('iam', 'CreateLoginProfile', async () => {
    const resp = await client.send(new CreateLoginProfileCommand({
      UserName: ctx.userName,
      Password: 'TempPassword123!',
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest('iam', 'GetLoginProfile', async () => {
    const resp = await client.send(new GetLoginProfileCommand({ UserName: ctx.userName }));
    if (!resp.LoginProfile) throw new Error('login profile to be defined');
  }));

  results.push(await runner.runTest('iam', 'UpdateUser', async () => {
    const newUserName = `UpdatedUser-${ts}`;
    await client.send(new UpdateUserCommand({ UserName: ctx.userName, NewUserName: newUserName }));
    ctx.userName = newUserName;
  }));

  results.push(await runner.runTest('iam', 'TagUser', async () => {
    await client.send(new TagUserCommand({
      UserName: ctx.userName,
      Tags: [{ Key: 'Environment', Value: 'test' }],
    }));
  }));

  results.push(await runner.runTest('iam', 'ListUserTags', async () => {
    const resp = await client.send(new ListUserTagsCommand({ UserName: ctx.userName }));
    if (!resp.Tags) throw new Error('tags to be defined');
  }));

  results.push(await runner.runTest('iam', 'UntagUser', async () => {
    await client.send(new UntagUserCommand({
      UserName: ctx.userName,
      TagKeys: ['Environment'],
    }));
  }));

  results.push(await runner.runTest('iam', 'UpdateAccessKey', async () => {
    await client.send(new UpdateAccessKeyCommand({
      AccessKeyId: ctx.accessKeyId,
      Status: 'Inactive',
      UserName: ctx.userName,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetAccessKeyLastUsed', async () => {
    const resp = await client.send(new GetAccessKeyLastUsedCommand({ AccessKeyId: ctx.accessKeyId }));
    if (!resp.UserName) throw new Error('username to be defined');
  }));

  results.push(await runner.runTest('iam', 'UpdateLoginProfile', async () => {
    await client.send(new UpdateLoginProfileCommand({
      UserName: ctx.userName,
      Password: 'NewPassword456!',
      PasswordResetRequired: true,
    }));
  }));

  results.push(await runner.runTest('iam', 'ListUsers_Pagination', async () => {
    const pgTs = String(Date.now());
    const pgUsers: string[] = [];
    for (const i of [0, 1, 2, 3, 4]) {
      const name = `PagUser-${pgTs}-${i}`;
      try {
        await client.send(new CreateUserCommand({ UserName: name }));
        pgUsers.push(name);
      } catch (err) {
        for (const n of pgUsers) {
          await safeCleanup(async () => { await client.send(new DeleteUserCommand({ UserName: n })); });
        }
        throw err;
      }
    }

    const allUsers: string[] = [];
    let marker: string | undefined;
    try {
      do {
        const resp = await client.send(new ListUsersCommand({
          PathPrefix: '/',
          Marker: marker,
          MaxItems: 2,
        }));
        if (!resp.Users) throw new Error('expected Users to be defined');
        for (const u of resp.Users) {
          if (u.UserName && u.UserName.startsWith(`PagUser-${pgTs}`)) {
            allUsers.push(u.UserName);
          }
        }
        marker = (resp.IsTruncated) ? resp.Marker : undefined;
      } while (marker);
    } finally {
      for (const n of pgUsers) {
        await safeCleanup(async () => { await client.send(new DeleteUserCommand({ UserName: n })); });
      }
    }

    if (allUsers.length !== 5) {
      throw new Error(`expected 5 paginated users, got ${allUsers.length}`);
    }
  }));

  results.push(await runner.runTest('iam', 'Error_DeleteNonExistentUser', async () => {
    try {
      await client.send(new DeleteUserCommand({ UserName: `NonExistentUser-${ts}` }));
      throw new Error('expected NoSuchEntity error');
    } catch (err) {
      assertErrorContains(err, 'NoSuchEntity');
    }
  }));

  results.push(await runner.runTest('iam', 'Error_CreateDuplicateUser', async () => {
    try {
      await client.send(new CreateUserCommand({ UserName: ctx.userName }));
      throw new Error('expected EntityAlreadyExists error');
    } catch (err) {
      assertErrorContains(err, 'EntityAlreadyExists');
    }
  }));

  return results;
}
