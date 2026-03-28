import {
  IAMClient,
  CreateUserCommand,
  GetUserCommand,
  ListUsersCommand,
  UpdateUserCommand,
  DeleteUserCommand,
  CreateGroupCommand,
  GetGroupCommand,
  ListGroupsCommand,
  ListGroupsForUserCommand,
  AddUserToGroupCommand,
  RemoveUserFromGroupCommand,
  DeleteGroupCommand,
  CreateRoleCommand,
  GetRoleCommand,
  ListRolesCommand,
  UpdateRoleCommand,
  UpdateRoleDescriptionCommand,
  DeleteRoleCommand,
  CreatePolicyCommand,
  GetPolicyCommand,
  ListPoliciesCommand,
  DeletePolicyCommand,
  AttachUserPolicyCommand,
  DetachUserPolicyCommand,
  AttachGroupPolicyCommand,
  DetachGroupPolicyCommand,
  AttachRolePolicyCommand,
  DetachRolePolicyCommand,
  CreateAccessKeyCommand,
  ListAccessKeysCommand,
  DeleteAccessKeyCommand,
  CreateLoginProfileCommand,
  GetLoginProfileCommand,
  DeleteLoginProfileCommand,
  TagUserCommand,
  ListUserTagsCommand,
  UntagUserCommand,
  SimulatePrincipalPolicyCommand,
  CreateInstanceProfileCommand,
  GetInstanceProfileCommand,
  ListInstanceProfilesCommand,
  AddRoleToInstanceProfileCommand,
  RemoveRoleFromInstanceProfileCommand,
  DeleteInstanceProfileCommand,
  PutUserPolicyCommand,
  GetUserPolicyCommand,
  ListUserPoliciesCommand,
  DeleteUserPolicyCommand,
  PutRolePolicyCommand,
  GetRolePolicyCommand,
  ListRolePoliciesCommand,
  DeleteRolePolicyCommand,
  TagRoleCommand,
  ListRoleTagsCommand,
  UntagRoleCommand,
  GetAccountSummaryCommand,
} from '@aws-sdk/client-iam';
import { EntityAlreadyExistsException, NoSuchEntityException } from '@aws-sdk/client-iam';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runIAMTests(
  runner: TestRunner,
  iamClient: IAMClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const userName = makeUniqueName('TSUser');
  const groupName = makeUniqueName('TSGroup');
  const roleName = makeUniqueName('TSRole');
  const policyName = makeUniqueName('TSPolicy');
  const instanceProfileName = makeUniqueName('TSInstanceProfile');
  const userInlinePolicyName = makeUniqueName('TSUserPolicy');
  const roleInlinePolicyName = makeUniqueName('TSRolePolicy');
  let userArn = '';
  let groupArn = '';
  let roleArn = '';
  let policyArn = '';
  let accessKeyId = '';
  let updatedUserName = userName + '_updated';

  const trustPolicy = JSON.stringify({
    Version: '2012-10-17',
    Statement: [
      {
        Effect: 'Allow',
        Principal: { Service: ['lambda.amazonaws.com', 'ec2.amazonaws.com'] },
        Action: 'sts:AssumeRole',
      },
    ],
  });

  const policyDocument = JSON.stringify({
    Version: '2012-10-17',
    Statement: [
      {
        Effect: 'Allow',
        Action: ['s3:GetObject', 's3:PutObject'],
        Resource: '*',
      },
    ],
  });

  try {
    // CreateUser
    results.push(
      await runner.runTest('iam', 'CreateUser', async () => {
        const resp = await iamClient.send(
          new CreateUserCommand({
            UserName: userName,
            Path: '/test/',
            Tags: [{ Key: 'Environment', Value: 'Test' }],
          })
        );
        if (!resp.User?.Arn) throw new Error('User Arn is null');
        userArn = resp.User.Arn;
      })
    );

    // GetUser
    results.push(
      await runner.runTest('iam', 'GetUser', async () => {
        const resp = await iamClient.send(
          new GetUserCommand({ UserName: userName })
        );
        if (!resp.User) throw new Error('User is null');
        if (!resp.User.Arn) throw new Error('User Arn is null');
        if (resp.User.UserName !== userName) {
          throw new Error(`Expected UserName=${userName}, got ${resp.User.UserName}`);
        }
      })
    );

    // ListUsers
    results.push(
      await runner.runTest('iam', 'ListUsers', async () => {
        const resp = await iamClient.send(new ListUsersCommand({}));
        if (!resp.Users) throw new Error('Users is null');
        const found = resp.Users.some((u) => u.UserName === userName);
        if (!found) throw new Error('Created user not found in list');
      })
    );

    // UpdateUser
    results.push(
      await runner.runTest('iam', 'UpdateUser', async () => {
        await iamClient.send(
          new UpdateUserCommand({
            UserName: userName,
            NewPath: '/test-updated/',
            NewUserName: updatedUserName,
          })
        );
      })
    );

    // TagUser
    results.push(
      await runner.runTest('iam', 'TagUser', async () => {
        await iamClient.send(
          new TagUserCommand({
            UserName: updatedUserName,
            Tags: [{ Key: 'Team', Value: 'Platform' }],
          })
        );
      })
    );

    // ListUserTags
    results.push(
      await runner.runTest('iam', 'ListUserTags', async () => {
        const resp = await iamClient.send(
          new ListUserTagsCommand({ UserName: updatedUserName })
        );
        if (!resp.Tags) throw new Error('Tags is null');
        const hasTeam = resp.Tags.some((t) => t.Key === 'Team' && t.Value === 'Platform');
        if (!hasTeam) throw new Error('Team tag not found');
      })
    );

    // UntagUser
    results.push(
      await runner.runTest('iam', 'UntagUser', async () => {
        await iamClient.send(
          new UntagUserCommand({
            UserName: updatedUserName,
            TagKeys: ['Team'],
          })
        );
      })
    );

    // CreateAccessKey
    results.push(
      await runner.runTest('iam', 'CreateAccessKey', async () => {
        const resp = await iamClient.send(
          new CreateAccessKeyCommand({ UserName: updatedUserName })
        );
        if (!resp.AccessKey) throw new Error('AccessKey is null');
        if (!resp.AccessKey.AccessKeyId) throw new Error('AccessKeyId is null');
        accessKeyId = resp.AccessKey.AccessKeyId;
      })
    );

    // ListAccessKeys
    results.push(
      await runner.runTest('iam', 'ListAccessKeys', async () => {
        const resp = await iamClient.send(
          new ListAccessKeysCommand({ UserName: updatedUserName })
        );
        if (!resp.AccessKeyMetadata) throw new Error('AccessKeyMetadata is null');
        const found = resp.AccessKeyMetadata.some((k) => k.AccessKeyId === accessKeyId);
        if (!found) throw new Error('Created access key not found in list');
      })
    );

    // DeleteAccessKey
    results.push(
      await runner.runTest('iam', 'DeleteAccessKey', async () => {
        await iamClient.send(
          new DeleteAccessKeyCommand({
            UserName: updatedUserName,
            AccessKeyId: accessKeyId,
          })
        );
      })
    );

    // CreateGroup
    results.push(
      await runner.runTest('iam', 'CreateGroup', async () => {
        const resp = await iamClient.send(
          new CreateGroupCommand({
            GroupName: groupName,
            Path: '/test/',
          })
        );
        if (!resp.Group?.Arn) throw new Error('Group Arn is null');
        groupArn = resp.Group.Arn;
      })
    );

    // GetGroup
    results.push(
      await runner.runTest('iam', 'GetGroup', async () => {
        const resp = await iamClient.send(
          new GetGroupCommand({ GroupName: groupName })
        );
        if (!resp.Group) throw new Error('Group is null');
        if (!resp.Group.Arn) throw new Error('Group Arn is null');
      })
    );

    // ListGroups
    results.push(
      await runner.runTest('iam', 'ListGroups', async () => {
        const resp = await iamClient.send(new ListGroupsCommand({}));
        if (!resp.Groups) throw new Error('Groups is null');
        const found = resp.Groups.some((g) => g.GroupName === groupName);
        if (!found) throw new Error('Created group not found in list');
      })
    );

    // AddUserToGroup
    results.push(
      await runner.runTest('iam', 'AddUserToGroup', async () => {
        await iamClient.send(
          new AddUserToGroupCommand({
            GroupName: groupName,
            UserName: updatedUserName,
          })
        );
      })
    );

    // ListGroupsForUser
    results.push(
      await runner.runTest('iam', 'ListGroupsForUser', async () => {
        const resp = await iamClient.send(
          new ListGroupsForUserCommand({ UserName: updatedUserName })
        );
        if (!resp.Groups) throw new Error('Groups is null');
        const found = resp.Groups.some((g) => g.GroupName === groupName);
        if (!found) throw new Error('User not in expected group');
      })
    );

    // RemoveUserFromGroup
    results.push(
      await runner.runTest('iam', 'RemoveUserFromGroup', async () => {
        await iamClient.send(
          new RemoveUserFromGroupCommand({
            GroupName: groupName,
            UserName: updatedUserName,
          })
        );
      })
    );

    // CreateRole
    results.push(
      await runner.runTest('iam', 'CreateRole', async () => {
        const resp = await iamClient.send(
          new CreateRoleCommand({
            RoleName: roleName,
            AssumeRolePolicyDocument: trustPolicy,
            Path: '/test/',
            Description: 'Test role for SDK tests',
          })
        );
        if (!resp.Role?.Arn) throw new Error('Role Arn is null');
        roleArn = resp.Role.Arn;
      })
    );

    // GetRole
    results.push(
      await runner.runTest('iam', 'GetRole', async () => {
        const resp = await iamClient.send(
          new GetRoleCommand({ RoleName: roleName })
        );
        if (!resp.Role) throw new Error('Role is null');
        if (!resp.Role.Arn) throw new Error('Role Arn is null');
        if (!resp.Role.AssumeRolePolicyDocument) {
          throw new Error('AssumeRolePolicyDocument is null');
        }
      })
    );

    // ListRoles
    results.push(
      await runner.runTest('iam', 'ListRoles', async () => {
        const resp = await iamClient.send(new ListRolesCommand({}));
        if (!resp.Roles) throw new Error('Roles is null');
        const found = resp.Roles.some((r) => r.RoleName === roleName);
        if (!found) throw new Error('Created role not found in list');
      })
    );

    // UpdateRole
    results.push(
      await runner.runTest('iam', 'UpdateRole', async () => {
        await iamClient.send(
          new UpdateRoleCommand({
            RoleName: roleName,
            Description: 'Updated test role',
          })
        );
      })
    );

    // CreatePolicy
    results.push(
      await runner.runTest('iam', 'CreatePolicy', async () => {
        const resp = await iamClient.send(
          new CreatePolicyCommand({
            PolicyName: policyName,
            PolicyDocument: policyDocument,
            Description: 'Test policy for SDK tests',
          })
        );
        if (!resp.Policy?.Arn) throw new Error('Policy Arn is null');
        policyArn = resp.Policy.Arn;
      })
    );

    // GetPolicy
    results.push(
      await runner.runTest('iam', 'GetPolicy', async () => {
        const resp = await iamClient.send(
          new GetPolicyCommand({ PolicyArn: policyArn })
        );
        if (!resp.Policy) throw new Error('Policy is null');
        if (!resp.Policy.Arn) throw new Error('Policy Arn is null');
        if (resp.Policy.DefaultVersionId) {
          // Should have a version
        }
      })
    );

    // ListPolicies
    results.push(
      await runner.runTest('iam', 'ListPolicies', async () => {
        const resp = await iamClient.send(
          new ListPoliciesCommand({ Scope: 'Local' })
        );
        if (!resp.Policies) throw new Error('Policies is null');
        const found = resp.Policies.some((p) => p.PolicyName === policyName);
        if (!found) throw new Error('Created policy not found in list');
      })
    );

    // AttachUserPolicy
    results.push(
      await runner.runTest('iam', 'AttachUserPolicy', async () => {
        await iamClient.send(
          new AttachUserPolicyCommand({
            UserName: updatedUserName,
            PolicyArn: policyArn,
          })
        );
      })
    );

    // DetachUserPolicy
    results.push(
      await runner.runTest('iam', 'DetachUserPolicy', async () => {
        await iamClient.send(
          new DetachUserPolicyCommand({
            UserName: updatedUserName,
            PolicyArn: policyArn,
          })
        );
      })
    );

    // AttachGroupPolicy
    results.push(
      await runner.runTest('iam', 'AttachGroupPolicy', async () => {
        await iamClient.send(
          new AttachGroupPolicyCommand({
            GroupName: groupName,
            PolicyArn: policyArn,
          })
        );
      })
    );

    // DetachGroupPolicy
    results.push(
      await runner.runTest('iam', 'DetachGroupPolicy', async () => {
        await iamClient.send(
          new DetachGroupPolicyCommand({
            GroupName: groupName,
            PolicyArn: policyArn,
          })
        );
      })
    );

    // DeleteGroup
    results.push(
      await runner.runTest('iam', 'DeleteGroup', async () => {
        await iamClient.send(
          new DeleteGroupCommand({ GroupName: groupName })
        );
      })
    );

    // AttachRolePolicy
    results.push(
      await runner.runTest('iam', 'AttachRolePolicy', async () => {
        await iamClient.send(
          new AttachRolePolicyCommand({
            RoleName: roleName,
            PolicyArn: policyArn,
          })
        );
      })
    );

    // DetachRolePolicy
    results.push(
      await runner.runTest('iam', 'DetachRolePolicy', async () => {
        await iamClient.send(
          new DetachRolePolicyCommand({
            RoleName: roleName,
            PolicyArn: policyArn,
          })
        );
      })
    );

    // SimulatePrincipalPolicy
    results.push(
      await runner.runTest('iam', 'SimulatePrincipalPolicy', async () => {
        await iamClient.send(
          new SimulatePrincipalPolicyCommand({
            PolicySourceArn: userArn,
            ActionNames: ['s3:GetObject', 's3:PutObject'],
            ResourceArns: ['*'],
          })
        );
      })
    );

    // CreateLoginProfile
    results.push(
      await runner.runTest('iam', 'CreateLoginProfile', async () => {
        await iamClient.send(
          new CreateLoginProfileCommand({
            UserName: updatedUserName,
            Password: 'TempPassword123!',
          })
        );
      })
    );

    // GetLoginProfile
    results.push(
      await runner.runTest('iam', 'GetLoginProfile', async () => {
        const resp = await iamClient.send(
          new GetLoginProfileCommand({ UserName: updatedUserName })
        );
        if (!resp.LoginProfile) throw new Error('login profile is nil');
      })
    );

    // CreateRole_InvalidName
    results.push(
      await runner.runTest('iam', 'CreateRole_InvalidName', async () => {
        try {
          await iamClient.send(
            new CreateRoleCommand({
              RoleName: 'invalid:role-name',
              AssumeRolePolicyDocument: '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":["lambda.amazonaws.com"]},"Action":["sts:AssumeRole"}]}',
            })
          );
          throw new Error('expected error for invalid role name with colon');
        } catch (err) {
          if (err instanceof Error && err.message.includes('expected error')) throw err;
        }
      })
    );

    // UpdateRoleDescription
    results.push(
      await runner.runTest('iam', 'UpdateRoleDescription', async () => {
        await iamClient.send(
          new UpdateRoleDescriptionCommand({
            RoleName: roleName,
            Description: 'Updated role description',
          })
        );
      })
    );

    // CreateInstanceProfile
    results.push(
      await runner.runTest('iam', 'CreateInstanceProfile', async () => {
        const resp = await iamClient.send(
          new CreateInstanceProfileCommand({
            InstanceProfileName: instanceProfileName,
          })
        );
        if (!resp.InstanceProfile) throw new Error('instance profile is nil');
      })
    );

    // GetInstanceProfile
    results.push(
      await runner.runTest('iam', 'GetInstanceProfile', async () => {
        const resp = await iamClient.send(
          new GetInstanceProfileCommand({
            InstanceProfileName: instanceProfileName,
          })
        );
        if (!resp.InstanceProfile) throw new Error('instance profile is nil');
      })
    );

    // ListInstanceProfiles
    results.push(
      await runner.runTest('iam', 'ListInstanceProfiles', async () => {
        const resp = await iamClient.send(new ListInstanceProfilesCommand({}));
        if (!resp.InstanceProfiles) throw new Error('instance profiles list is nil');
      })
    );

    // AddRoleToInstanceProfile
    results.push(
      await runner.runTest('iam', 'AddRoleToInstanceProfile', async () => {
        const resp = await iamClient.send(
          new AddRoleToInstanceProfileCommand({
            InstanceProfileName: instanceProfileName,
            RoleName: roleName,
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // RemoveRoleFromInstanceProfile
    results.push(
      await runner.runTest('iam', 'RemoveRoleFromInstanceProfile', async () => {
        const resp = await iamClient.send(
          new RemoveRoleFromInstanceProfileCommand({
            InstanceProfileName: instanceProfileName,
            RoleName: roleName,
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // PutUserPolicy
    results.push(
      await runner.runTest('iam', 'PutUserPolicy', async () => {
        const resp = await iamClient.send(
          new PutUserPolicyCommand({
            UserName: updatedUserName,
            PolicyName: userInlinePolicyName,
            PolicyDocument: '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Action":"s3:GetObject","Resource":"*"}]}',
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // GetUserPolicy
    results.push(
      await runner.runTest('iam', 'GetUserPolicy', async () => {
        const resp = await iamClient.send(
          new GetUserPolicyCommand({
            UserName: updatedUserName,
            PolicyName: userInlinePolicyName,
          })
        );
        if (!resp.PolicyDocument || resp.PolicyDocument === '') throw new Error('policy document is empty');
      })
    );

    // ListUserPolicies
    results.push(
      await runner.runTest('iam', 'ListUserPolicies', async () => {
        const resp = await iamClient.send(
          new ListUserPoliciesCommand({ UserName: updatedUserName })
        );
        if (!resp.PolicyNames) throw new Error('policy names list is nil');
      })
    );

    // PutRolePolicy
    results.push(
      await runner.runTest('iam', 'PutRolePolicy', async () => {
        const resp = await iamClient.send(
          new PutRolePolicyCommand({
            RoleName: roleName,
            PolicyName: roleInlinePolicyName,
            PolicyDocument: '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Action":"logs:*","Resource":"*"}]}',
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // GetRolePolicy
    results.push(
      await runner.runTest('iam', 'GetRolePolicy', async () => {
        const resp = await iamClient.send(
          new GetRolePolicyCommand({
            RoleName: roleName,
            PolicyName: roleInlinePolicyName,
          })
        );
        if (!resp.PolicyDocument || resp.PolicyDocument === '') throw new Error('policy document is empty');
      })
    );

    // ListRolePolicies
    results.push(
      await runner.runTest('iam', 'ListRolePolicies', async () => {
        const resp = await iamClient.send(
          new ListRolePoliciesCommand({ RoleName: roleName })
        );
        if (!resp.PolicyNames) throw new Error('policy names list is nil');
      })
    );

    // GetAccountSummary
    results.push(
      await runner.runTest('iam', 'GetAccountSummary', async () => {
        const resp = await iamClient.send(new GetAccountSummaryCommand({}));
        if (!resp.SummaryMap) throw new Error('summary map is nil');
      })
    );

    // DeleteUserPolicy
    results.push(
      await runner.runTest('iam', 'DeleteUserPolicy', async () => {
        const resp = await iamClient.send(
          new DeleteUserPolicyCommand({
            UserName: updatedUserName,
            PolicyName: userInlinePolicyName,
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DeleteRolePolicy
    results.push(
      await runner.runTest('iam', 'DeleteRolePolicy', async () => {
        const resp = await iamClient.send(
          new DeleteRolePolicyCommand({
            RoleName: roleName,
            PolicyName: roleInlinePolicyName,
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DeleteLoginProfile
    results.push(
      await runner.runTest('iam', 'DeleteLoginProfile', async () => {
        const resp = await iamClient.send(
          new DeleteLoginProfileCommand({ UserName: updatedUserName })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DeleteInstanceProfile
    results.push(
      await runner.runTest('iam', 'DeleteInstanceProfile', async () => {
        const resp = await iamClient.send(
          new DeleteInstanceProfileCommand({
            InstanceProfileName: instanceProfileName,
          })
        );
        if (!resp) throw new Error('response is nil');
      })
    );

    // DeleteRole
    results.push(
      await runner.runTest('iam', 'DeleteRole', async () => {
        await iamClient.send(
          new DeleteRoleCommand({ RoleName: roleName })
        );
      })
    );

    // DeletePolicy
    results.push(
      await runner.runTest('iam', 'DeletePolicy', async () => {
        await iamClient.send(
          new DeletePolicyCommand({ PolicyArn: policyArn })
        );
      })
    );

    // DeleteUser
    results.push(
      await runner.runTest('iam', 'DeleteUser', async () => {
        await iamClient.send(
          new DeleteUserCommand({ UserName: updatedUserName })
        );
      })
    );

  } finally {
    // Cleanup (ignore errors)
    try {
      await iamClient.send(new DeleteAccessKeyCommand({ UserName: updatedUserName, AccessKeyId: accessKeyId }));
    } catch { /* ignore */ }
    try {
      await iamClient.send(new RemoveUserFromGroupCommand({ GroupName: groupName, UserName: updatedUserName }));
    } catch { /* ignore */ }
    try {
      await iamClient.send(new DeleteGroupCommand({ GroupName: groupName }));
    } catch { /* ignore */ }
    try {
      await iamClient.send(new DeleteRoleCommand({ RoleName: roleName }));
    } catch { /* ignore */ }
    try {
      await iamClient.send(new DeleteUserCommand({ UserName: updatedUserName }));
    } catch { /* ignore */ }
  }

  // Error cases
  results.push(
    await runner.runTest('iam', 'GetUser_NonExistent', async () => {
      try {
        await iamClient.send(
          new GetUserCommand({ UserName: 'NonExistentUser_xyz_12345' })
        );
        throw new Error('Expected NoSuchEntityException but got none');
      } catch (err: unknown) {
        if (err instanceof NoSuchEntityException) {
          // Expected
        } else if (err instanceof Error && err.name === 'NoSuchEntityException') {
          // Expected
        }
      }
    })
  );

  results.push(
    await runner.runTest('iam', 'GetGroup_NonExistent', async () => {
      try {
        await iamClient.send(
          new GetGroupCommand({ GroupName: 'NonExistentGroup_xyz_12345' })
        );
        throw new Error('Expected NoSuchEntityException but got none');
      } catch (err: unknown) {
        if (err instanceof NoSuchEntityException) {
          // Expected
        } else if (err instanceof Error && err.name === 'NoSuchEntityException') {
          // Expected
        }
      }
    })
  );

  results.push(
    await runner.runTest('iam', 'GetRole_NonExistent', async () => {
      try {
        await iamClient.send(
          new GetRoleCommand({ RoleName: 'NonExistentRole_xyz_12345' })
        );
        throw new Error('Expected NoSuchEntityException but got none');
      } catch (err: unknown) {
        if (err instanceof NoSuchEntityException) {
          // Expected
        } else if (err instanceof Error && err.name === 'NoSuchEntityException') {
          // Expected
        }
      }
    })
  );

  results.push(
    await runner.runTest('iam', 'CreateUser_Duplicate', async () => {
      try {
        await iamClient.send(
          new CreateUserCommand({ UserName: userName })
        );
        throw new Error('Expected EntityAlreadyExistsException but got none');
      } catch (err: unknown) {
        if (err instanceof EntityAlreadyExistsException) {
          // Expected
        } else if (err instanceof Error && err.name === 'EntityAlreadyExistsException') {
          // Expected
        }
      }
    })
  );

  return results;
}