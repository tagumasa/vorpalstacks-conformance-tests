import {
  CreateRoleCommand,
  GetRoleCommand,
  ListRolesCommand,
  UpdateRoleDescriptionCommand,
  UpdateRoleCommand,
  UpdateAssumeRolePolicyCommand,
  TagRoleCommand,
  ListRoleTagsCommand,
  UntagRoleCommand,
  PutRolePolicyCommand,
  GetRolePolicyCommand,
  ListRolePoliciesCommand,
  DeleteRolePolicyCommand,
  CreateServiceLinkedRoleCommand,
  DeleteServiceLinkedRoleCommand,
  GetServiceLinkedRoleDeletionStatusCommand,
  DeleteRoleCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runRoleTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('iam', 'CreateRole', async () => {
    const assumeRolePolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Principal: { Service: 'lambda.amazonaws.com' }, Action: 'sts:AssumeRole' }],
    });
    const resp = await client.send(new CreateRoleCommand({
      RoleName: ctx.roleName,
      AssumeRolePolicyDocument: assumeRolePolicy,
    }));
    if (!resp.Role) throw new Error('role to be defined');
    if (!resp.Role.Arn) throw new Error('role arn to be defined');
  }));

  results.push(await runner.runTest('iam', 'CreateRole_InvalidName', async () => {
    try {
      await client.send(new CreateRoleCommand({
        RoleName: 'invalid:role-name',
        AssumeRolePolicyDocument: JSON.stringify({ Version: '2012-10-17', Statement: [{ Effect: 'Allow', Principal: { Service: ['lambda.amazonaws.com'] }, Action: ['sts:AssumeRole'] }] }),
      }));
      throw new Error('expected error for invalid role name with colon');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for invalid role name with colon') throw err;
    }
  }));

  results.push(await runner.runTest('iam', 'GetRole', async () => {
    const resp = await client.send(new GetRoleCommand({ RoleName: ctx.roleName }));
    if (!resp.Role) throw new Error('role to be defined');
  }));

  results.push(await runner.runTest('iam', 'ListRoles', async () => {
    const resp = await client.send(new ListRolesCommand({}));
    if (!resp.Roles) throw new Error('roles list to be defined');
  }));

  results.push(await runner.runTest('iam', 'UpdateRoleDescription', async () => {
    await client.send(new UpdateRoleDescriptionCommand({
      RoleName: ctx.roleName,
      Description: 'Updated role description',
    }));
  }));

  results.push(await runner.runTest('iam', 'UpdateRole', async () => {
    await client.send(new UpdateRoleCommand({
      RoleName: ctx.roleName,
      MaxSessionDuration: 3600,
    }));
  }));

  results.push(await runner.runTest('iam', 'UpdateAssumeRolePolicy', async () => {
    const newTrustPolicy = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Principal: { Service: 'ec2.amazonaws.com' }, Action: 'sts:AssumeRole' }],
    });
    await client.send(new UpdateAssumeRolePolicyCommand({
      RoleName: ctx.roleName,
      PolicyDocument: newTrustPolicy,
    }));
  }));

  results.push(await runner.runTest('iam', 'TagRole', async () => {
    await client.send(new TagRoleCommand({
      RoleName: ctx.roleName,
      Tags: [{ Key: 'Environment', Value: 'test' }],
    }));
  }));

  results.push(await runner.runTest('iam', 'ListRoleTags', async () => {
    const resp = await client.send(new ListRoleTagsCommand({ RoleName: ctx.roleName }));
    if (!resp.Tags) throw new Error('tags to be defined');
  }));

  results.push(await runner.runTest('iam', 'UntagRole', async () => {
    await client.send(new UntagRoleCommand({
      RoleName: ctx.roleName,
      TagKeys: ['Environment'],
    }));
  }));

  const rolePolicyDoc = JSON.stringify({
    Version: '2012-10-17',
    Statement: [{ Effect: 'Allow', Action: 'logs:*', Resource: '*' }],
  });

  results.push(await runner.runTest('iam', 'PutRolePolicy', async () => {
    await client.send(new PutRolePolicyCommand({
      RoleName: ctx.roleName,
      PolicyName: ctx.roleInlinePolicyName,
      PolicyDocument: rolePolicyDoc,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetRolePolicy', async () => {
    const resp = await client.send(new GetRolePolicyCommand({
      RoleName: ctx.roleName,
      PolicyName: ctx.roleInlinePolicyName,
    }));
    if (!resp.PolicyDocument || resp.PolicyDocument === '') throw new Error('policy document is empty');
  }));

  results.push(await runner.runTest('iam', 'ListRolePolicies', async () => {
    const resp = await client.send(new ListRolePoliciesCommand({ RoleName: ctx.roleName }));
    if (!resp.PolicyNames) throw new Error('policy names list to be defined');
  }));

  results.push(await runner.runTest('iam', 'DeleteRolePolicy', async () => {
    await client.send(new DeleteRolePolicyCommand({
      RoleName: ctx.roleName,
      PolicyName: ctx.roleInlinePolicyName,
    }));
  }));

  results.push(await runner.runTest('iam', 'Error_GetNonExistentRole', async () => {
    try {
      await client.send(new GetRoleCommand({ RoleName: `NonExistentRole-${ts}` }));
      throw new Error('expected NoSuchEntity error');
    } catch (err) {
      assertErrorContains(err, 'NoSuchEntity');
    }
  }));

  results.push(await runner.runTest('iam', 'CreateServiceLinkedRole', async () => {
    const resp = await client.send(new CreateServiceLinkedRoleCommand({
      AWSServiceName: 'lambda.amazonaws.com',
      Description: 'Test service-linked role',
    }));
    if (!resp.Role) throw new Error('role to be defined');
    if (!resp.Role.RoleName) throw new Error('role name to be defined');
    ctx.serviceLinkedRoleName = resp.Role.RoleName;
  }));

  results.push(await runner.runTest('iam', 'DeleteServiceLinkedRole', async () => {
    const resp = await client.send(new DeleteServiceLinkedRoleCommand({ RoleName: ctx.serviceLinkedRoleName }));
    if (!resp.DeletionTaskId) throw new Error('deletion task id to be defined');
  }));

  results.push(await runner.runTest('iam', 'GetServiceLinkedRoleDeletionStatus', async () => {
    await client.send(new GetServiceLinkedRoleDeletionStatusCommand({ DeletionTaskId: 'test-task-id' }));
  }));

  results.push(await runner.runTest('iam', 'ListRoles_Pagination', async () => {
    const pgTs = String(Date.now());
    const pgRoles: string[] = [];
    for (const i of [0, 1, 2, 3, 4]) {
      const name = `PagRole-${pgTs}-${i}`;
      await client.send(new CreateRoleCommand({
        RoleName: name,
        AssumeRolePolicyDocument: JSON.stringify({ Version: '2012-10-17', Statement: [{ Effect: 'Allow', Principal: { Service: 'lambda.amazonaws.com' }, Action: 'sts:AssumeRole' }] }),
      }));
      pgRoles.push(name);
    }

    const allRoles: string[] = [];
    let marker: string | undefined;
    while (true) {
      const resp = await client.send(new ListRolesCommand({ PathPrefix: '/', Marker: marker, MaxItems: 2 }));
      for (const r of resp.Roles ?? []) {
        if (r.RoleName?.startsWith(`PagRole-${pgTs}`)) allRoles.push(r.RoleName);
      }
      if (resp.IsTruncated && resp.Marker) { marker = resp.Marker; } else { break; }
    }

    for (const name of pgRoles) {
      await safeCleanup(() => client.send(new DeleteRoleCommand({ RoleName: name })));
    }
    if (allRoles.length !== 5) throw new Error(`expected 5 paginated roles, got ${allRoles.length}`);
  }));

  return results;
}
