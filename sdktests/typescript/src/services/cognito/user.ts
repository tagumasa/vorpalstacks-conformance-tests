import {
  CognitoIdentityProviderClient,
  AdminCreateUserCommand,
  AdminGetUserCommand,
  AdminDeleteUserCommand,
  AdminDisableUserCommand,
  AdminEnableUserCommand,
  AdminUpdateUserAttributesCommand,
  AdminDeleteUserAttributesCommand,
  AdminResetUserPasswordCommand,
  AdminSetUserPasswordCommand,
  AdminInitiateAuthCommand,
  AdminUserGlobalSignOutCommand,
  GlobalSignOutCommand,
  SignUpCommand,
  ConfirmSignUpCommand,
  ListUsersCommand,
  CreateGroupCommand,
  DeleteGroupCommand,
  GetGroupCommand,
  UpdateGroupCommand,
  ListGroupsCommand,
  AdminAddUserToGroupCommand,
  AdminRemoveUserFromGroupCommand,
  ListUsersInGroupCommand,
  AdminListGroupsForUserCommand,
  CreateUserPoolClientCommand,
  DeleteUserPoolClientCommand,
  CreateUserPoolCommand,
  DeleteUserPoolCommand,
} from '@aws-sdk/client-cognito-identity-provider';
import type { TestRunner, TestResult } from '../../runner.js';
import { safeCleanup } from '../../helpers.js';

const SVC = 'cognito';
const uniqueName = (prefix: string) => `${prefix}-${Date.now()}-${Math.floor(Math.random() * 99999)}`;

export async function runUserTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
  userPoolId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const username = uniqueName('user');
  results.push(await runner.runTest(SVC, 'AdminCreateUser', async () => {
    const resp = await client.send(new AdminCreateUserCommand({
      UserPoolId: userPoolId,
      Username: username,
      TemporaryPassword: 'TempPass123!',
      MessageAction: 'SUPPRESS',
    }));
    if (resp.User?.Username !== username) throw new Error(`username mismatch: ${resp.User?.Username}`);
    if (resp.User?.UserStatus !== 'FORCE_CHANGE_PASSWORD') throw new Error(`expected FORCE_CHANGE_PASSWORD, got ${resp.User?.UserStatus}`);
  }));

  results.push(await runner.runTest(SVC, 'AdminGetUser', async () => {
    const resp = await client.send(new AdminGetUserCommand({ UserPoolId: userPoolId, Username: username }));
    if (resp.Username !== username) throw new Error(`username mismatch: ${resp.Username}`);
    if (!resp.Enabled) throw new Error('expected user to be enabled');
  }));

  results.push(await runner.runTest(SVC, 'ListUsers', async () => {
    const resp = await client.send(new ListUsersCommand({ UserPoolId: userPoolId }));
    if (!resp.Users?.length) throw new Error('expected at least one user');
  }));

  results.push(await runner.runTest(SVC, 'AdminDisableUser', async () => {
    await client.send(new AdminDisableUserCommand({ UserPoolId: userPoolId, Username: username }));
  }));

  results.push(await runner.runTest(SVC, 'AdminEnableUser', async () => {
    await client.send(new AdminEnableUserCommand({ UserPoolId: userPoolId, Username: username }));
  }));

  results.push(await runner.runTest(SVC, 'AdminUpdateUserAttributes', async () => {
    const attrUser = uniqueName('attr2-user');
    await client.send(new AdminCreateUserCommand({
      UserPoolId: userPoolId,
      Username: attrUser,
      TemporaryPassword: 'TempPass123!',
      MessageAction: 'SUPPRESS',
    }));
    try {
      await client.send(new AdminUpdateUserAttributesCommand({
        UserPoolId: userPoolId,
        Username: attrUser,
        UserAttributes: [
          { Name: 'email', Value: 'updated@example.com' },
          { Name: 'phone_number', Value: '+441234567890' },
        ],
      }));
      const getResp = await client.send(new AdminGetUserCommand({ UserPoolId: userPoolId, Username: attrUser }));
      const found = getResp.UserAttributes?.some(a => a.Name === 'email' && a.Value === 'updated@example.com');
      if (!found) throw new Error('updated email attribute not found');
    } finally {
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: attrUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminDeleteUserAttributes', async () => {
    const daUser = uniqueName('da-user');
    await client.send(new AdminCreateUserCommand({
      UserPoolId: userPoolId,
      Username: daUser,
      TemporaryPassword: 'TempPass123!',
      MessageAction: 'SUPPRESS',
      UserAttributes: [
        { Name: 'email', Value: 'da@example.com' },
        { Name: 'name', Value: 'DA User' },
      ],
    }));
    try {
      await client.send(new AdminDeleteUserAttributesCommand({
        UserPoolId: userPoolId,
        Username: daUser,
        UserAttributeNames: ['name'],
      }));
      const getResp = await client.send(new AdminGetUserCommand({ UserPoolId: userPoolId, Username: daUser }));
      const stillExists = getResp.UserAttributes?.some(a => a.Name === 'name');
      if (stillExists) throw new Error("attribute 'name' should have been deleted");
    } finally {
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: daUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminResetUserPassword', async () => {
    const rpUser = uniqueName('rp-user');
    await client.send(new AdminCreateUserCommand({
      UserPoolId: userPoolId,
      Username: rpUser,
      TemporaryPassword: 'TempPass123!',
      MessageAction: 'SUPPRESS',
    }));
    try {
      await client.send(new AdminSetUserPasswordCommand({
        UserPoolId: userPoolId,
        Username: rpUser,
        Password: 'PermPass123!',
        Permanent: true,
      }));
      await client.send(new AdminResetUserPasswordCommand({ UserPoolId: userPoolId, Username: rpUser }));
      const getResp = await client.send(new AdminGetUserCommand({ UserPoolId: userPoolId, Username: rpUser }));
      if (getResp.UserStatus !== 'FORCE_CHANGE_PASSWORD' && getResp.UserStatus !== 'RESET_REQUIRED') {
        throw new Error(`expected FORCE_CHANGE_PASSWORD or RESET_REQUIRED, got ${getResp.UserStatus}`);
      }
    } finally {
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: rpUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminSetUserPassword', async () => {
    const spUser = uniqueName('sp-user');
    await client.send(new AdminCreateUserCommand({
      UserPoolId: userPoolId,
      Username: spUser,
      TemporaryPassword: 'TempPass123!',
      MessageAction: 'SUPPRESS',
    }));
    try {
      await client.send(new AdminSetUserPasswordCommand({
        UserPoolId: userPoolId,
        Username: spUser,
        Password: 'NewPermPass123!',
        Permanent: true,
      }));
      const getResp = await client.send(new AdminGetUserCommand({ UserPoolId: userPoolId, Username: spUser }));
      if (getResp.UserStatus !== 'CONFIRMED') throw new Error(`expected CONFIRMED, got ${getResp.UserStatus}`);
    } finally {
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: spUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminDeleteUser', async () => {
    await client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: username }));
  }));

  return results;
}

export async function runGroupTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
  userPoolId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const groupName = uniqueName('group');
  results.push(await runner.runTest(SVC, 'CreateGroup', async () => {
    const resp = await client.send(new CreateGroupCommand({ GroupName: groupName, UserPoolId: userPoolId }));
    if (resp.Group?.GroupName !== groupName) throw new Error(`name mismatch: ${resp.Group?.GroupName}`);
    if (resp.Group?.UserPoolId !== userPoolId) throw new Error(`pool ID mismatch: ${resp.Group?.UserPoolId}`);
  }));

  results.push(await runner.runTest(SVC, 'ListGroups', async () => {
    const resp = await client.send(new ListGroupsCommand({ UserPoolId: userPoolId }));
    if (!resp.Groups?.length) throw new Error('expected at least one group');
  }));

  results.push(await runner.runTest(SVC, 'DeleteGroup', async () => {
    await client.send(new DeleteGroupCommand({ GroupName: groupName, UserPoolId: userPoolId }));
  }));

  results.push(await runner.runTest(SVC, 'GetGroup', async () => {
    const gName = uniqueName('get-group');
    await client.send(new CreateGroupCommand({ GroupName: gName, UserPoolId: userPoolId }));
    try {
      const resp = await client.send(new GetGroupCommand({ GroupName: gName, UserPoolId: userPoolId }));
      if (resp.Group?.GroupName !== gName) throw new Error(`name mismatch: ${resp.Group?.GroupName}`);
      if (resp.Group?.UserPoolId !== userPoolId) throw new Error(`pool ID mismatch: ${resp.Group?.UserPoolId}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteGroupCommand({ GroupName: gName, UserPoolId: userPoolId })));
    }
  }));

  results.push(await runner.runTest(SVC, 'UpdateGroup', async () => {
    const ugName = uniqueName('ug-group');
    await client.send(new CreateGroupCommand({ GroupName: ugName, UserPoolId: userPoolId, Description: 'Original' }));
    try {
      await client.send(new UpdateGroupCommand({ GroupName: ugName, UserPoolId: userPoolId, Description: 'Updated', Precedence: 10 }));
      const resp = await client.send(new GetGroupCommand({ GroupName: ugName, UserPoolId: userPoolId }));
      if (resp.Group?.Description !== 'Updated') throw new Error(`description not updated: ${resp.Group?.Description}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteGroupCommand({ GroupName: ugName, UserPoolId: userPoolId })));
    }
  }));

  return results;
}

export async function runAuthAndGroupMembershipTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
  userPoolId: string,
  clientId: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'SignUp', async () => {
    const signUpClientName = uniqueName('signup-client');
    const signUpClientResp = await client.send(new CreateUserPoolClientCommand({ UserPoolId: userPoolId, ClientName: signUpClientName }));
    const signUpClientId = signUpClientResp.UserPoolClient!.ClientId!;
    try {
      const signUpUser = uniqueName('signup-user');
      const resp = await client.send(new SignUpCommand({
        ClientId: signUpClientId,
        Username: signUpUser,
        Password: 'SignUpPass123!',
        UserAttributes: [{ Name: 'email', Value: 'signup@example.com' }],
      }));
      if (!resp.UserSub) throw new Error('expected UserSub to be defined');
      if (resp.UserConfirmed) throw new Error('expected UserConfirmed=false');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolClientCommand({ ClientId: signUpClientId, UserPoolId: userPoolId })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ConfirmSignUp', async () => {
    const confirmClientName = uniqueName('confirm-client');
    const confirmClientResp = await client.send(new CreateUserPoolClientCommand({ UserPoolId: userPoolId, ClientName: confirmClientName }));
    const confirmClientId = confirmClientResp.UserPoolClient!.ClientId!;
    try {
      const confirmUser = uniqueName('confirm-user');
      await client.send(new SignUpCommand({ ClientId: confirmClientId, Username: confirmUser, Password: 'ConfirmPass123!' }));
      await client.send(new ConfirmSignUpCommand({ ClientId: confirmClientId, Username: confirmUser, ConfirmationCode: '123456' }));
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolClientCommand({ ClientId: confirmClientId, UserPoolId: userPoolId })));
    }
  }));

  if (clientId) {
    results.push(await runner.runTest(SVC, 'AdminInitiateAuth', async () => {
      const authUser = uniqueName('auth-user');
      await client.send(new AdminCreateUserCommand({
        UserPoolId: userPoolId,
        Username: authUser,
        TemporaryPassword: 'TempPass123!',
        MessageAction: 'SUPPRESS',
      }));
      try {
        await client.send(new AdminSetUserPasswordCommand({
          UserPoolId: userPoolId,
          Username: authUser,
          Password: 'AuthPass123!',
          Permanent: true,
        }));
        const authResp = await client.send(new AdminInitiateAuthCommand({
          UserPoolId: userPoolId,
          ClientId: clientId,
          AuthFlow: 'ADMIN_NO_SRP_AUTH',
          AuthParameters: { USERNAME: authUser, PASSWORD: 'AuthPass123!' },
        }));
        if (!authResp.AuthenticationResult) throw new Error('expected AuthenticationResult');
        if (!authResp.AuthenticationResult.AccessToken) throw new Error('expected AccessToken');
        if (!authResp.AuthenticationResult.IdToken) throw new Error('expected IdToken');
      } finally {
        await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: authUser })));
      }
    }));
  }

  results.push(await runner.runTest(SVC, 'AdminAddUserToGroup', async () => {
    const ugUser = uniqueName('ug-user');
    const ugGroup = uniqueName('ug-group');
    await client.send(new AdminCreateUserCommand({ UserPoolId: userPoolId, Username: ugUser, TemporaryPassword: 'TempPass123!', MessageAction: 'SUPPRESS' }));
    await client.send(new CreateGroupCommand({ GroupName: ugGroup, UserPoolId: userPoolId }));
    try {
      await client.send(new AdminAddUserToGroupCommand({ UserPoolId: userPoolId, GroupName: ugGroup, Username: ugUser }));
    } finally {
      await safeCleanup(() => client.send(new DeleteGroupCommand({ GroupName: ugGroup, UserPoolId: userPoolId })));
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: ugUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListUsersInGroup', async () => {
    const ugUser = uniqueName('ug2-user');
    const ugGroup = uniqueName('ug2-group');
    await client.send(new AdminCreateUserCommand({ UserPoolId: userPoolId, Username: ugUser, TemporaryPassword: 'TempPass123!', MessageAction: 'SUPPRESS' }));
    await client.send(new CreateGroupCommand({ GroupName: ugGroup, UserPoolId: userPoolId }));
    try {
      await client.send(new AdminAddUserToGroupCommand({ UserPoolId: userPoolId, GroupName: ugGroup, Username: ugUser }));
      const listResp = await client.send(new ListUsersInGroupCommand({ UserPoolId: userPoolId, GroupName: ugGroup }));
      const found = listResp.Users?.some(u => u.Username === ugUser);
      if (!found) throw new Error('user not found in ListUsersInGroup');
      await client.send(new AdminRemoveUserFromGroupCommand({ UserPoolId: userPoolId, GroupName: ugGroup, Username: ugUser }));
      const listResp2 = await client.send(new ListUsersInGroupCommand({ UserPoolId: userPoolId, GroupName: ugGroup }));
      const stillThere = listResp2.Users?.some(u => u.Username === ugUser);
      if (stillThere) throw new Error('user still in group after AdminRemoveUserFromGroup');
    } finally {
      await safeCleanup(() => client.send(new DeleteGroupCommand({ GroupName: ugGroup, UserPoolId: userPoolId })));
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: ugUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminListGroupsForUser', async () => {
    const lgUser = uniqueName('lg-user');
    const lgGroup1 = uniqueName('lg-group1');
    const lgGroup2 = uniqueName('lg-group2');
    await client.send(new AdminCreateUserCommand({ UserPoolId: userPoolId, Username: lgUser, TemporaryPassword: 'TempPass123!', MessageAction: 'SUPPRESS' }));
    await client.send(new CreateGroupCommand({ GroupName: lgGroup1, UserPoolId: userPoolId }));
    await client.send(new CreateGroupCommand({ GroupName: lgGroup2, UserPoolId: userPoolId }));
    try {
      await client.send(new AdminAddUserToGroupCommand({ UserPoolId: userPoolId, GroupName: lgGroup1, Username: lgUser }));
      await client.send(new AdminAddUserToGroupCommand({ UserPoolId: userPoolId, GroupName: lgGroup2, Username: lgUser }));
      const resp = await client.send(new AdminListGroupsForUserCommand({ UserPoolId: userPoolId, Username: lgUser }));
      if ((resp.Groups?.length ?? 0) < 2) throw new Error(`expected at least 2 groups, got ${resp.Groups?.length}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteGroupCommand({ GroupName: lgGroup1, UserPoolId: userPoolId })));
      await safeCleanup(() => client.send(new DeleteGroupCommand({ GroupName: lgGroup2, UserPoolId: userPoolId })));
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: lgUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminUserGlobalSignOut', async () => {
    const gsoUser = uniqueName('gso-user');
    await client.send(new AdminCreateUserCommand({ UserPoolId: userPoolId, Username: gsoUser, TemporaryPassword: 'TempPass123!', MessageAction: 'SUPPRESS' }));
    try {
      await client.send(new AdminSetUserPasswordCommand({ UserPoolId: userPoolId, Username: gsoUser, Password: 'GSOPass123!', Permanent: true }));
      await client.send(new AdminUserGlobalSignOutCommand({ UserPoolId: userPoolId, Username: gsoUser }));
    } finally {
      await safeCleanup(() => client.send(new AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: gsoUser })));
    }
  }));

  results.push(await runner.runTest(SVC, 'GlobalSignOut', async () => {
    let err: unknown;
    try {
      await client.send(new GlobalSignOutCommand({ AccessToken: 'dummy-token' }));
    } catch (e) { err = e; }
    if (!err) throw new Error('expected error for dummy access token');
  }));

  return results;
}

export async function runUserErrorTests(
  runner: TestRunner,
  client: CognitoIdentityProviderClient,
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  results.push(await runner.runTest(SVC, 'AdminGetUser_NonExistent', async () => {
    const poolName = uniqueName('err-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      let err: unknown;
      try {
        await client.send(new AdminGetUserCommand({ UserPoolId: createResp.UserPool!.Id!, Username: 'nonexistent-user-xyz' }));
      } catch (e) { err = e; }
      if (!err) throw new Error('expected error for non-existent user');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'AdminCreateUser_VerifyAttributes', async () => {
    const poolName = uniqueName('attr-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      const attrUser = uniqueName('attr-user');
      const resp = await client.send(new AdminCreateUserCommand({
        UserPoolId: createResp.UserPool!.Id!,
        Username: attrUser,
        TemporaryPassword: 'TempPass123!',
        MessageAction: 'SUPPRESS',
        UserAttributes: [
          { Name: 'email', Value: 'test@example.com' },
          { Name: 'name', Value: 'Test User' },
        ],
      }));
      if (resp.User?.Username !== attrUser) throw new Error('username mismatch');
      if (!resp.User.Enabled) throw new Error('user should be enabled');
      if (resp.User.UserStatus !== 'FORCE_CHANGE_PASSWORD') throw new Error(`expected FORCE_CHANGE_PASSWORD, got ${resp.User.UserStatus}`);
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListUsers_ContainsCreated', async () => {
    const poolName = uniqueName('list-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      const listUser = uniqueName('list-user');
      await client.send(new AdminCreateUserCommand({
        UserPoolId: createResp.UserPool!.Id!,
        Username: listUser,
        TemporaryPassword: 'TempPass123!',
        MessageAction: 'SUPPRESS',
      }));
      const resp = await client.send(new ListUsersCommand({ UserPoolId: createResp.UserPool!.Id! }));
      const found = resp.Users?.some(u => u.Username === listUser);
      if (!found) throw new Error('created user not found in ListUsers');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  results.push(await runner.runTest(SVC, 'ListGroups_ContainsCreated', async () => {
    const poolName = uniqueName('grp-pool');
    const createResp = await client.send(new CreateUserPoolCommand({ PoolName: poolName }));
    try {
      const testGroup = uniqueName('test-grp');
      await client.send(new CreateGroupCommand({ GroupName: testGroup, UserPoolId: createResp.UserPool!.Id!, Description: 'Test group description' }));
      const resp = await client.send(new ListGroupsCommand({ UserPoolId: createResp.UserPool!.Id! }));
      const found = resp.Groups?.find(g => g.GroupName === testGroup);
      if (!found) throw new Error('created group not found in ListGroups');
      if (found.Description !== 'Test group description') throw new Error('group description mismatch');
    } finally {
      await safeCleanup(() => client.send(new DeleteUserPoolCommand({ UserPoolId: createResp.UserPool!.Id! })));
    }
  }));

  return results;
}
