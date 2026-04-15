import {
  CreateInstanceProfileCommand,
  GetInstanceProfileCommand,
  ListInstanceProfilesCommand,
  ListInstanceProfilesForRoleCommand,
  AddRoleToInstanceProfileCommand,
  RemoveRoleFromInstanceProfileCommand,
  TagInstanceProfileCommand,
  ListInstanceProfileTagsCommand,
  UntagInstanceProfileCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';

export async function runInstanceProfileTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'CreateInstanceProfile', async () => {
    const resp = await client.send(new CreateInstanceProfileCommand({ InstanceProfileName: ctx.profileName }));
    if (!resp.InstanceProfile) throw new Error('instance profile to be defined');
  }));

  results.push(await runner.runTest('iam', 'GetInstanceProfile', async () => {
    const resp = await client.send(new GetInstanceProfileCommand({ InstanceProfileName: ctx.profileName }));
    if (!resp.InstanceProfile) throw new Error('instance profile to be defined');
  }));

  results.push(await runner.runTest('iam', 'ListInstanceProfiles', async () => {
    const resp = await client.send(new ListInstanceProfilesCommand({}));
    if (!resp.InstanceProfiles) throw new Error('instance profiles list to be defined');
  }));

  results.push(await runner.runTest('iam', 'ListInstanceProfilesForRole', async () => {
    const resp = await client.send(new ListInstanceProfilesForRoleCommand({ RoleName: ctx.roleName }));
    if (!resp.InstanceProfiles) throw new Error('instance profiles list to be defined');
  }));

  results.push(await runner.runTest('iam', 'AddRoleToInstanceProfile', async () => {
    await client.send(new AddRoleToInstanceProfileCommand({
      InstanceProfileName: ctx.profileName,
      RoleName: ctx.roleName,
    }));
  }));

  results.push(await runner.runTest('iam', 'RemoveRoleFromInstanceProfile', async () => {
    await client.send(new RemoveRoleFromInstanceProfileCommand({
      InstanceProfileName: ctx.profileName,
      RoleName: ctx.roleName,
    }));
  }));

  results.push(await runner.runTest('iam', 'TagInstanceProfile', async () => {
    await client.send(new TagInstanceProfileCommand({
      InstanceProfileName: ctx.profileName,
      Tags: [{ Key: 'Environment', Value: 'test' }],
    }));
  }));

  results.push(await runner.runTest('iam', 'ListInstanceProfileTags', async () => {
    const resp = await client.send(new ListInstanceProfileTagsCommand({ InstanceProfileName: ctx.profileName }));
    if (!resp.Tags) throw new Error('tags to be defined');
  }));

  results.push(await runner.runTest('iam', 'UntagInstanceProfile', async () => {
    await client.send(new UntagInstanceProfileCommand({
      InstanceProfileName: ctx.profileName,
      TagKeys: ['Environment'],
    }));
  }));

  return results;
}
