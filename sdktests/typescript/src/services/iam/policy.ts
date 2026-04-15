import {
  CreatePolicyCommand,
  GetPolicyCommand,
  ListPoliciesCommand,
  TagPolicyCommand,
  ListPolicyTagsCommand,
  UntagPolicyCommand,
  AttachUserPolicyCommand,
  ListAttachedUserPoliciesCommand,
  DetachUserPolicyCommand,
  PutUserPermissionsBoundaryCommand,
  DeleteUserPermissionsBoundaryCommand,
  AttachGroupPolicyCommand,
  ListAttachedGroupPoliciesCommand,
  ListEntitiesForPolicyCommand,
  DetachGroupPolicyCommand,
  PutGroupPolicyCommand,
  GetGroupPolicyCommand,
  ListGroupPoliciesCommand,
  DeleteGroupPolicyCommand,
  AttachRolePolicyCommand,
  CreatePolicyVersionCommand,
  ListPolicyVersionsCommand,
  SetDefaultPolicyVersionCommand,
  GetPolicyVersionCommand,
  DeletePolicyVersionCommand,
  PutUserPolicyCommand,
  GetUserPolicyCommand,
  ListUserPoliciesCommand,
  DeleteUserPolicyCommand,
  GetUserCommand,
  DeletePolicyCommand,
  ListAttachedRolePoliciesCommand,
  DetachRolePolicyCommand,
  PutRolePermissionsBoundaryCommand,
  DeleteRolePermissionsBoundaryCommand,
  GetRoleCommand,
} from '@aws-sdk/client-iam';
import { IAMTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runPolicyTests(ctx: IAMTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  const policyDocument = JSON.stringify({
    Version: '2012-10-17',
    Statement: [{ Effect: 'Allow', Action: 's3:*', Resource: '*' }],
  });

  results.push(await runner.runTest('iam', 'CreatePolicy', async () => {
    const resp = await client.send(new CreatePolicyCommand({
      PolicyName: ctx.policyName,
      PolicyDocument: policyDocument,
    }));
    if (!resp.Policy) throw new Error('policy to be defined');
    if (!resp.Policy.Arn) throw new Error('policy arn to be defined');
    ctx.policyArn = resp.Policy.Arn;
  }));

  results.push(await runner.runTest('iam', 'GetPolicy', async () => {
    const resp = await client.send(new GetPolicyCommand({ PolicyArn: ctx.policyArn }));
    if (!resp.Policy) throw new Error('policy to be defined');
    if (resp.Policy.PolicyName !== ctx.policyName) {
      throw new Error(`policy name mismatch: got ${resp.Policy.PolicyName}, want ${ctx.policyName}`);
    }
  }));

  results.push(await runner.runTest('iam', 'ListPolicies', async () => {
    const resp = await client.send(new ListPoliciesCommand({}));
    if (!resp.Policies) throw new Error('policies list to be defined');
  }));

  results.push(await runner.runTest('iam', 'TagPolicy', async () => {
    await client.send(new TagPolicyCommand({
      PolicyArn: ctx.policyArn,
      Tags: [{ Key: 'Environment', Value: 'test' }],
    }));
  }));

  results.push(await runner.runTest('iam', 'ListPolicyTags', async () => {
    const resp = await client.send(new ListPolicyTagsCommand({ PolicyArn: ctx.policyArn }));
    if (!resp.Tags) throw new Error('tags to be defined');
  }));

  results.push(await runner.runTest('iam', 'UntagPolicy', async () => {
    await client.send(new UntagPolicyCommand({ PolicyArn: ctx.policyArn, TagKeys: ['Environment'] }));
  }));

  results.push(await runner.runTest('iam', 'AttachUserPolicy', async () => {
    await client.send(new AttachUserPolicyCommand({ UserName: ctx.userName, PolicyArn: ctx.policyArn }));
  }));

  results.push(await runner.runTest('iam', 'ListAttachedUserPolicies', async () => {
    const resp = await client.send(new ListAttachedUserPoliciesCommand({ UserName: ctx.userName }));
    if (!resp.AttachedPolicies) throw new Error('attached policies list to be defined');
    const found = resp.AttachedPolicies.some((p) => p.PolicyArn === ctx.policyArn);
    if (!found) throw new Error(`policy ${ctx.policyArn} not found in ListAttachedUserPolicies`);
  }));

  results.push(await runner.runTest('iam', 'DetachUserPolicy', async () => {
    await client.send(new DetachUserPolicyCommand({ UserName: ctx.userName, PolicyArn: ctx.policyArn }));
  }));

  results.push(await runner.runTest('iam', 'PutUserPermissionsBoundary', async () => {
    await client.send(new PutUserPermissionsBoundaryCommand({
      UserName: ctx.userName,
      PermissionsBoundary: ctx.policyArn,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetUser_PermissionsBoundary', async () => {
    const resp = await client.send(new GetUserCommand({ UserName: ctx.userName }));
    if (!resp.User) throw new Error('user to be defined');
    if (!resp.User.PermissionsBoundary) throw new Error('permissions boundary to be defined');
    if (resp.User.PermissionsBoundary.PermissionsBoundaryArn !== ctx.policyArn) {
      throw new Error('permissions boundary arn mismatch');
    }
  }));

  results.push(await runner.runTest('iam', 'DeleteUserPermissionsBoundary', async () => {
    await client.send(new DeleteUserPermissionsBoundaryCommand({ UserName: ctx.userName }));
  }));

  results.push(await runner.runTest('iam', 'AttachGroupPolicy', async () => {
    await client.send(new AttachGroupPolicyCommand({ GroupName: ctx.groupName, PolicyArn: ctx.policyArn }));
  }));

  results.push(await runner.runTest('iam', 'ListAttachedGroupPolicies', async () => {
    const resp = await client.send(new ListAttachedGroupPoliciesCommand({ GroupName: ctx.groupName }));
    if (!resp.AttachedPolicies) throw new Error('attached policies list to be defined');
    const found = resp.AttachedPolicies.some((p) => p.PolicyArn === ctx.policyArn);
    if (!found) throw new Error(`policy ${ctx.policyArn} not found in ListAttachedGroupPolicies`);
  }));

  results.push(await runner.runTest('iam', 'ListEntitiesForPolicy_Role', async () => {
    await client.send(new AttachRolePolicyCommand({ RoleName: ctx.roleName, PolicyArn: ctx.policyArn }));
    const resp = await client.send(new ListEntitiesForPolicyCommand({ PolicyArn: ctx.policyArn }));
    if (!resp.PolicyRoles) throw new Error('policy roles list to be defined');
    if (!resp.PolicyGroups) throw new Error('policy groups list to be defined');
  }));

  results.push(await runner.runTest('iam', 'DetachGroupPolicy', async () => {
    await client.send(new DetachGroupPolicyCommand({ GroupName: ctx.groupName, PolicyArn: ctx.policyArn }));
  }));

  results.push(await runner.runTest('iam', 'AttachRolePolicy_FullCycle', async () => {
    await client.send(new AttachRolePolicyCommand({ RoleName: ctx.roleName, PolicyArn: ctx.policyArn }));
  }));

  results.push(await runner.runTest('iam', 'ListAttachedRolePolicies', async () => {
    const resp = await client.send(new ListAttachedRolePoliciesCommand({ RoleName: ctx.roleName }));
    if (!resp.AttachedPolicies) throw new Error('attached policies list to be defined');
    const found = resp.AttachedPolicies.some((p) => p.PolicyArn === ctx.policyArn);
    if (!found) throw new Error(`policy ${ctx.policyArn} not found in ListAttachedRolePolicies`);
  }));

  results.push(await runner.runTest('iam', 'DetachRolePolicy', async () => {
    await client.send(new DetachRolePolicyCommand({ RoleName: ctx.roleName, PolicyArn: ctx.policyArn }));
  }));

  results.push(await runner.runTest('iam', 'PutRolePermissionsBoundary', async () => {
    await client.send(new PutRolePermissionsBoundaryCommand({
      RoleName: ctx.roleName,
      PermissionsBoundary: ctx.policyArn,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetRole_PermissionsBoundary', async () => {
    const resp = await client.send(new GetRoleCommand({ RoleName: ctx.roleName }));
    if (!resp.Role) throw new Error('role to be defined');
    if (!resp.Role.PermissionsBoundary) throw new Error('permissions boundary to be defined');
    if (resp.Role.PermissionsBoundary.PermissionsBoundaryArn !== ctx.policyArn) {
      throw new Error('permissions boundary arn mismatch');
    }
  }));

  results.push(await runner.runTest('iam', 'DeleteRolePermissionsBoundary', async () => {
    await client.send(new DeleteRolePermissionsBoundaryCommand({ RoleName: ctx.roleName }));
  }));

  const groupPolicyDoc = JSON.stringify({
    Version: '2012-10-17',
    Statement: [{ Effect: 'Allow', Action: 'logs:*', Resource: '*' }],
  });

  results.push(await runner.runTest('iam', 'PutGroupPolicy', async () => {
    await client.send(new PutGroupPolicyCommand({
      GroupName: ctx.groupName,
      PolicyName: ctx.groupInlinePolicyName,
      PolicyDocument: groupPolicyDoc,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetGroupPolicy', async () => {
    const resp = await client.send(new GetGroupPolicyCommand({
      GroupName: ctx.groupName,
      PolicyName: ctx.groupInlinePolicyName,
    }));
    if (!resp.PolicyDocument || resp.PolicyDocument === '') throw new Error('policy document is empty');
  }));

  results.push(await runner.runTest('iam', 'ListGroupPolicies', async () => {
    const resp = await client.send(new ListGroupPoliciesCommand({ GroupName: ctx.groupName }));
    if (!resp.PolicyNames) throw new Error('policy names list to be defined');
  }));

  results.push(await runner.runTest('iam', 'DeleteGroupPolicy', async () => {
    await client.send(new DeleteGroupPolicyCommand({
      GroupName: ctx.groupName,
      PolicyName: ctx.groupInlinePolicyName,
    }));
  }));

  const userPolicyDoc = JSON.stringify({
    Version: '2012-10-17',
    Statement: [{ Effect: 'Allow', Action: 's3:GetObject', Resource: '*' }],
  });

  results.push(await runner.runTest('iam', 'PutUserPolicy', async () => {
    await client.send(new PutUserPolicyCommand({
      UserName: ctx.userName,
      PolicyName: ctx.userInlinePolicyName,
      PolicyDocument: userPolicyDoc,
    }));
  }));

  results.push(await runner.runTest('iam', 'GetUserPolicy', async () => {
    const resp = await client.send(new GetUserPolicyCommand({
      UserName: ctx.userName,
      PolicyName: ctx.userInlinePolicyName,
    }));
    if (!resp.PolicyDocument || resp.PolicyDocument === '') throw new Error('policy document is empty');
  }));

  results.push(await runner.runTest('iam', 'ListUserPolicies', async () => {
    const resp = await client.send(new ListUserPoliciesCommand({ UserName: ctx.userName }));
    if (!resp.PolicyNames) throw new Error('policy names list to be defined');
  }));

  results.push(await runner.runTest('iam', 'DeleteUserPolicy', async () => {
    await client.send(new DeleteUserPolicyCommand({
      UserName: ctx.userName,
      PolicyName: ctx.userInlinePolicyName,
    }));
  }));

  // ========== POLICY VERSIONING ==========

  results.push(await runner.runTest('iam', 'CreatePolicyVersion', async () => {
    const v2Document = JSON.stringify({
      Version: '2012-10-17',
      Statement: [{ Effect: 'Allow', Action: 'ec2:*', Resource: '*' }],
    });
    const resp = await client.send(new CreatePolicyVersionCommand({
      PolicyArn: ctx.policyArn,
      PolicyDocument: v2Document,
      SetAsDefault: false,
    }));
    if (!resp.PolicyVersion) throw new Error('policy version to be defined');
    if (!resp.PolicyVersion.VersionId) throw new Error('version id to be defined');
  }));

  results.push(await runner.runTest('iam', 'ListPolicyVersions', async () => {
    const resp = await client.send(new ListPolicyVersionsCommand({ PolicyArn: ctx.policyArn }));
    if (!resp.Versions) throw new Error('versions list to be defined');
    if (resp.Versions.length < 2) throw new Error(`expected at least 2 policy versions, got ${resp.Versions.length}`);
  }));

  results.push(await runner.runTest('iam', 'SetDefaultPolicyVersion', async () => {
    const listResp = await client.send(new ListPolicyVersionsCommand({ PolicyArn: ctx.policyArn }));
    const nonDefault = listResp.Versions?.find((v) => !v.IsDefaultVersion && v.VersionId);
    if (!nonDefault || !nonDefault.VersionId) throw new Error('no non-default version found');
    await client.send(new SetDefaultPolicyVersionCommand({ PolicyArn: ctx.policyArn, VersionId: nonDefault.VersionId }));
  }));

  results.push(await runner.runTest('iam', 'GetPolicyVersion', async () => {
    const listResp = await client.send(new ListPolicyVersionsCommand({ PolicyArn: ctx.policyArn }));
    const defaultV = listResp.Versions?.find((v) => v.IsDefaultVersion && v.VersionId);
    if (!defaultV || !defaultV.VersionId) throw new Error('no default version found');
    const getResp = await client.send(new GetPolicyVersionCommand({
      PolicyArn: ctx.policyArn,
      VersionId: defaultV.VersionId,
    }));
    if (!getResp.PolicyVersion) throw new Error('policy version to be defined');
    if (!getResp.PolicyVersion.IsDefaultVersion) throw new Error('expected default version');
  }));

  results.push(await runner.runTest('iam', 'DeletePolicyVersion', async () => {
    const listResp = await client.send(new ListPolicyVersionsCommand({ PolicyArn: ctx.policyArn }));
    const nonDefault = listResp.Versions?.find((v) => !v.IsDefaultVersion && v.VersionId);
    if (!nonDefault || !nonDefault.VersionId) throw new Error('no non-default version found to delete');
    await client.send(new DeletePolicyVersionCommand({ PolicyArn: ctx.policyArn, VersionId: nonDefault.VersionId }));
  }));

  results.push(await runner.runTest('iam', 'Error_AttachPolicyToNonExistentUser', async () => {
    try {
      await client.send(new AttachUserPolicyCommand({
        UserName: `NonExistentUser-${ts}`,
        PolicyArn: ctx.policyArn,
      }));
      throw new Error('expected NoSuchEntity error');
    } catch (err) {
      assertErrorContains(err, 'NoSuchEntity');
    }
  }));

  results.push(await runner.runTest('iam', 'Error_DeleteDefaultPolicyVersion', async () => {
    const listResp = await client.send(new ListPolicyVersionsCommand({ PolicyArn: ctx.policyArn }));
    const defaultV = listResp.Versions?.find((v) => v.IsDefaultVersion && v.VersionId);
    if (!defaultV || !defaultV.VersionId) throw new Error('no default version found');
    try {
      await client.send(new DeletePolicyVersionCommand({
        PolicyArn: ctx.policyArn,
        VersionId: defaultV.VersionId,
      }));
      throw new Error('expected error when deleting default policy version');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      const code = (err as { Code?: string }).Code ?? '';
      if (!msg.includes('InvalidInput') && !msg.includes('DeleteConflict') && !code.includes('InvalidInput') && !code.includes('DeleteConflict')) {
        throw new Error(`expected InvalidInput or DeleteConflict error, got: ${msg}`);
      }
    }
  }));

  results.push(await runner.runTest('iam', 'Error_CreateDuplicatePolicy', async () => {
    try {
      await client.send(new CreatePolicyCommand({ PolicyName: ctx.policyName, PolicyDocument: policyDocument }));
      throw new Error('expected EntityAlreadyExists error');
    } catch (err) {
      assertErrorContains(err, 'EntityAlreadyExists');
    }
  }));

  results.push(await runner.runTest('iam', 'ListPolicies_Pagination', async () => {
    const pgTs = String(Date.now());
    const pgPolicyArns: string[] = [];
    for (const i of [0, 1, 2, 3, 4]) {
      const name = `PagPolicy-${pgTs}-${i}`;
      const resp = await client.send(new CreatePolicyCommand({
        PolicyName: name,
        PolicyDocument: JSON.stringify({ Version: '2012-10-17', Statement: [{ Effect: 'Allow', Action: '*', Resource: '*' }] }),
      }));
      pgPolicyArns.push(resp.Policy!.Arn!);
    }

    const allPolicies: string[] = [];
    let marker: string | undefined;
    while (true) {
      const resp = await client.send(new ListPoliciesCommand({ Scope: 'Local', Marker: marker, MaxItems: 2 }));
      for (const p of resp.Policies ?? []) {
        if (p.PolicyName?.startsWith(`PagPolicy-${pgTs}`)) allPolicies.push(p.PolicyName);
      }
      if (resp.IsTruncated && resp.Marker) { marker = resp.Marker; } else { break; }
    }

    for (const arn of pgPolicyArns) {
      await safeCleanup(() => client.send(new DeletePolicyCommand({ PolicyArn: arn })));
    }
    if (allPolicies.length !== 5) throw new Error(`expected 5 paginated policies, got ${allPolicies.length}`);
  }));

  return results;
}
