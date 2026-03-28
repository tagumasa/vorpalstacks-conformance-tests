import {
  CognitoIdentityProviderClient,
  CreateUserPoolCommand,
  DescribeUserPoolCommand,
  ListUserPoolsCommand,
  UpdateUserPoolCommand,
  DeleteUserPoolCommand,
  CreateUserPoolDomainCommand,
  DescribeUserPoolDomainCommand,
  DeleteUserPoolDomainCommand,
  AdminGetUserCommand,
  ListUsersCommand,
  AdminCreateUserCommand,
  AdminDisableUserCommand,
  AdminEnableUserCommand,
  AdminDeleteUserCommand,
  SignUpCommand,
  ConfirmSignUpCommand,
  CreateUserPoolClientCommand,
  DescribeUserPoolClientCommand,
  UpdateUserPoolClientCommand,
  ListUserPoolClientsCommand,
  CreateGroupCommand,
  ListGroupsCommand,
  DeleteGroupCommand,
  CreateResourceServerCommand,
  ListResourceServersCommand,
  CreateIdentityProviderCommand,
  ListIdentityProvidersCommand,
  SetUserPoolMfaConfigCommand,
  GetUserPoolMfaConfigCommand,
  GetCSVHeaderCommand,
  DescribeRiskConfigurationCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
  GlobalSignOutCommand,
  DeleteUserPoolClientCommand,
} from '@aws-sdk/client-cognito-identity-provider';
import { ResourceNotFoundException, UserNotFoundException } from '@aws-sdk/client-cognito-identity-provider';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runCognitoTests(
  runner: TestRunner,
  cognitoClient: CognitoIdentityProviderClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const userPoolName = makeUniqueName('TSUserPool');
  const domainName = makeUniqueName('tsuserpool');
  const testEmail = `test-${Date.now()}@example.com`;
  let userPoolId = '';
  let userPoolArn = '';
  let clientId = '';
  const clientName = makeUniqueName('TSClient');
  const groupName = makeUniqueName('TSGroup');
  const username = `user-${Date.now()}`;
  const identifier = `resource-${Date.now()}`;

  try {
    // CreateUserPool
    results.push(
      await runner.runTest('cognito', 'CreateUserPool', async () => {
        const resp = await cognitoClient.send(
          new CreateUserPoolCommand({
            PoolName: userPoolName,
            AutoVerifiedAttributes: ['email'],
            Policies: {
              PasswordPolicy: {
                MinimumLength: 8,
                RequireUppercase: true,
                RequireLowercase: true,
                RequireNumbers: true,
                RequireSymbols: false,
              },
            },
            Schema: [
              {
                Name: 'email',
                AttributeDataType: 'String',
                Required: true,
              },
            ],
          })
        );
        if (!resp.UserPool?.Id) throw new Error('UserPool Id is null');
        userPoolId = resp.UserPool.Id;
        userPoolArn = resp.UserPool.Arn || '';
      })
    );

    // DescribeUserPool
    results.push(
      await runner.runTest('cognito', 'DescribeUserPool', async () => {
        const resp = await cognitoClient.send(
          new DescribeUserPoolCommand({ UserPoolId: userPoolId })
        );
        if (!resp.UserPool) throw new Error('UserPool is null');
        if (!resp.UserPool.Id) throw new Error('UserPool Id is null');
      })
    );

    // CreateUserPoolClient
    results.push(
      await runner.runTest('cognito', 'CreateUserPoolClient', async () => {
        const resp = await cognitoClient.send(
          new CreateUserPoolClientCommand({
            UserPoolId: userPoolId,
            ClientName: clientName,
          })
        );
        if (!resp.UserPoolClient?.ClientId) throw new Error('UserPoolClient.ClientId is nil');
        clientId = resp.UserPoolClient.ClientId;
      })
    );

    // DescribeUserPoolClient
    if (clientId) {
      results.push(
        await runner.runTest('cognito', 'DescribeUserPoolClient', async () => {
          const resp = await cognitoClient.send(
            new DescribeUserPoolClientCommand({
              ClientId: clientId,
              UserPoolId: userPoolId,
            })
          );
          if (!resp.UserPoolClient) throw new Error('UserPoolClient is nil');
          if (resp.UserPoolClient.ClientId !== clientId) throw new Error('ClientId mismatch');
        })
      );

      // UpdateUserPoolClient
      results.push(
        await runner.runTest('cognito', 'UpdateUserPoolClient', async () => {
          const resp = await cognitoClient.send(
            new UpdateUserPoolClientCommand({
              ClientId: clientId,
              UserPoolId: userPoolId,
              ClientName: 'updated-client',
            })
          );
          if (!resp.UserPoolClient) throw new Error('UserPoolClient is nil');
          if (resp.UserPoolClient.ClientName !== 'updated-client') throw new Error('ClientName not updated');
        })
      );
    }

    // ListUserPools
    results.push(
      await runner.runTest('cognito', 'ListUserPools', async () => {
        const resp = await cognitoClient.send(
          new ListUserPoolsCommand({ MaxResults: 10 })
        );
        if (!resp.UserPools) throw new Error('UserPools is null');
      })
    );

    // UpdateUserPool
    results.push(
      await runner.runTest('cognito', 'UpdateUserPool', async () => {
        await cognitoClient.send(
          new UpdateUserPoolCommand({
            UserPoolId: userPoolId,
            Policies: {
              PasswordPolicy: {
                MinimumLength: 12,
                RequireUppercase: true,
                RequireLowercase: true,
                RequireNumbers: true,
                RequireSymbols: true,
              },
            },
          })
        );
      })
    );

    // CreateUserPoolDomain
    results.push(
      await runner.runTest('cognito', 'CreateUserPoolDomain', async () => {
        await cognitoClient.send(
          new CreateUserPoolDomainCommand({
            Domain: domainName,
            UserPoolId: userPoolId,
          })
        );
      })
    );

    // DescribeUserPoolDomain
    results.push(
      await runner.runTest('cognito', 'DescribeUserPoolDomain', async () => {
        const resp = await cognitoClient.send(
          new DescribeUserPoolDomainCommand({ Domain: domainName })
        );
        if (!resp.DomainDescription) throw new Error('DomainDescription is null');
        if (resp.DomainDescription.UserPoolId !== userPoolId) {
          throw new Error(`UserPoolId mismatch: expected ${userPoolId}, got ${resp.DomainDescription.UserPoolId}`);
        }
      })
    );

    // ListUserPoolClients
    results.push(
      await runner.runTest('cognito', 'ListUserPoolClients', async () => {
        const resp = await cognitoClient.send(
          new ListUserPoolClientsCommand({
            UserPoolId: userPoolId,
            MaxResults: 10,
          })
        );
        if (!resp.UserPoolClients || resp.UserPoolClients.length === 0) throw new Error('expected at least one user pool client');
      })
    );

    // CreateGroup
    results.push(
      await runner.runTest('cognito', 'CreateGroup', async () => {
        const resp = await cognitoClient.send(
          new CreateGroupCommand({
            GroupName: groupName,
            UserPoolId: userPoolId,
          })
        );
        if (!resp.Group) throw new Error('Group is nil');
        if (resp.Group.GroupName !== groupName) throw new Error('GroupName mismatch');
      })
    );

    // ListGroups
    results.push(
      await runner.runTest('cognito', 'ListGroups', async () => {
        const resp = await cognitoClient.send(
          new ListGroupsCommand({ UserPoolId: userPoolId })
        );
        if (!resp.Groups || resp.Groups.length === 0) throw new Error('expected at least one group');
      })
    );

    // AdminCreateUser
    results.push(
      await runner.runTest('cognito', 'AdminCreateUser', async () => {
        const resp = await cognitoClient.send(
          new AdminCreateUserCommand({
            UserPoolId: userPoolId,
            Username: username,
            TemporaryPassword: 'TempPass123!',
            MessageAction: 'SUPPRESS',
            UserAttributes: [{ Name: 'email', Value: 'test@example.com' }],
          })
        );
        if (!resp.User) throw new Error('User is null');
        if (resp.User.Username !== username) throw new Error('Username mismatch');
      })
    );

    // AdminGetUser
    results.push(
      await runner.runTest('cognito', 'AdminGetUser', async () => {
        const resp = await cognitoClient.send(
          new AdminGetUserCommand({
            UserPoolId: userPoolId,
            Username: username,
          })
        );
        if (!resp.Username) throw new Error('Username is null');
      })
    );

    // ListUsers
    results.push(
      await runner.runTest('cognito', 'ListUsers', async () => {
        const resp = await cognitoClient.send(
          new ListUsersCommand({ UserPoolId: userPoolId })
        );
        if (!resp.Users) throw new Error('Users is null');
        if (resp.Users.length < 1) throw new Error('Expected at least 1 user');
      })
    );

    // CreateResourceServer
    results.push(
      await runner.runTest('cognito', 'CreateResourceServer', async () => {
        const resp = await cognitoClient.send(
          new CreateResourceServerCommand({
            UserPoolId: userPoolId,
            Identifier: identifier,
            Name: 'Test Resource Server',
          })
        );
        if (!resp.ResourceServer) throw new Error('ResourceServer is nil');
        if (resp.ResourceServer.Identifier !== identifier) throw new Error('Identifier mismatch');
        if (resp.ResourceServer.Name !== 'Test Resource Server') throw new Error('Name mismatch');
      })
    );

    // ListResourceServers
    results.push(
      await runner.runTest('cognito', 'ListResourceServers', async () => {
        const resp = await cognitoClient.send(
          new ListResourceServersCommand({ UserPoolId: userPoolId })
        );
        if (!resp.ResourceServers || resp.ResourceServers.length === 0) throw new Error('expected at least one resource server');
      })
    );

    // CreateIdentityProvider
    results.push(
      await runner.runTest('cognito', 'CreateIdentityProvider', async () => {
        const resp = await cognitoClient.send(
          new CreateIdentityProviderCommand({
            UserPoolId: userPoolId,
            ProviderName: 'TestProvider',
            ProviderType: 'Facebook',
            ProviderDetails: {
              client_id: 'test-client-id',
              client_secret: 'test-client-secret',
              authorize_scopes: 'public_profile,email',
            },
          })
        );
        if (!resp.IdentityProvider) throw new Error('IdentityProvider is nil');
        if (resp.IdentityProvider.ProviderName !== 'TestProvider') throw new Error('ProviderName mismatch');
      })
    );

    // ListIdentityProviders
    results.push(
      await runner.runTest('cognito', 'ListIdentityProviders', async () => {
        const resp = await cognitoClient.send(
          new ListIdentityProvidersCommand({ UserPoolId: userPoolId })
        );
        if (!resp.Providers || resp.Providers.length === 0) throw new Error('expected at least one identity provider');
      })
    );

    // SetUserPoolMfaConfig
    results.push(
      await runner.runTest('cognito', 'SetUserPoolMfaConfig', async () => {
        await cognitoClient.send(
          new SetUserPoolMfaConfigCommand({
            UserPoolId: userPoolId,
            SmsMfaConfiguration: {
              SmsConfiguration: {
                SnsCallerArn: 'arn:aws:sns:us-east-1:123456789012:sms-topic',
                ExternalId: 'external-id',
              },
            },
          })
        );
      })
    );

    // GetUserPoolMfaConfig
    results.push(
      await runner.runTest('cognito', 'GetUserPoolMfaConfig', async () => {
        const resp = await cognitoClient.send(
          new GetUserPoolMfaConfigCommand({ UserPoolId: userPoolId })
        );
        if (!resp.MfaConfiguration && !resp.SoftwareTokenMfaConfiguration && !resp.SmsMfaConfiguration && !resp.EmailMfaConfiguration) {
          throw new Error('expected at least one MFA config field to be set');
        }
      })
    );

    // AdminDisableUser
    results.push(
      await runner.runTest('cognito', 'AdminDisableUser', async () => {
        await cognitoClient.send(
          new AdminDisableUserCommand({
            UserPoolId: userPoolId,
            Username: username,
          })
        );
      })
    );

    // AdminEnableUser
    results.push(
      await runner.runTest('cognito', 'AdminEnableUser', async () => {
        await cognitoClient.send(
          new AdminEnableUserCommand({
            UserPoolId: userPoolId,
            Username: username,
          })
        );
      })
    );

    // SignUp
    results.push(
      await runner.runTest('cognito', 'SignUp', async () => {
        if (!clientId) throw new Error('No ClientId available');
        await cognitoClient.send(
          new SignUpCommand({
            ClientId: clientId,
            Username: testEmail,
            Password: 'TestPassword123!',
            UserAttributes: [{ Name: 'email', Value: testEmail }],
          })
        );
      })
    );

    // ConfirmSignUp
    results.push(
      await runner.runTest('cognito', 'ConfirmSignUp', async () => {
        if (!clientId) throw new Error('No ClientId available');
        await cognitoClient.send(
          new ConfirmSignUpCommand({
            ClientId: clientId,
            Username: testEmail,
            ConfirmationCode: '123456',
          })
        );
      })
    );

    // AdminDeleteUser
    results.push(
      await runner.runTest('cognito', 'AdminDeleteUser', async () => {
        await cognitoClient.send(
          new AdminDeleteUserCommand({
            UserPoolId: userPoolId,
            Username: username,
          })
        );
      })
    );

    // DeleteUserPoolDomain
    results.push(
      await runner.runTest('cognito', 'DeleteUserPoolDomain', async () => {
        await cognitoClient.send(
          new DeleteUserPoolDomainCommand({
            Domain: domainName,
            UserPoolId: userPoolId,
          })
        );
      })
    );

    // DeleteUserPoolClient
    if (clientId) {
      results.push(
        await runner.runTest('cognito', 'DeleteUserPoolClient', async () => {
          await cognitoClient.send(
            new DeleteUserPoolClientCommand({
              ClientId: clientId,
              UserPoolId: userPoolId,
            })
          );
        })
      );
    }

    // DeleteGroup
    results.push(
      await runner.runTest('cognito', 'DeleteGroup', async () => {
        await cognitoClient.send(
          new DeleteGroupCommand({
            GroupName: groupName,
            UserPoolId: userPoolId,
          })
        );
      })
    );

    // GetCSVHeader
    results.push(
      await runner.runTest('cognito', 'GetCSVHeader', async () => {
        const resp = await cognitoClient.send(
          new GetCSVHeaderCommand({ UserPoolId: userPoolId })
        );
        if (!resp.CSVHeader || resp.CSVHeader.length === 0) throw new Error('expected non-empty CSV header');
      })
    );

    // DescribeRiskConfiguration
    results.push(
      await runner.runTest('cognito', 'DescribeRiskConfiguration', async () => {
        const resp = await cognitoClient.send(
          new DescribeRiskConfigurationCommand({ UserPoolId: userPoolId })
        );
        if (!resp.RiskConfiguration) throw new Error('RiskConfiguration is nil');
      })
    );

    // DeleteUserPool
    results.push(
      await runner.runTest('cognito', 'DeleteUserPool', async () => {
        await cognitoClient.send(
          new DeleteUserPoolCommand({ UserPoolId: userPoolId })
        );
      })
    );

    // TagResource
    if (userPoolArn) {
      results.push(
        await runner.runTest('cognito', 'TagResource', async () => {
          const newPool = makeUniqueName('TSTagPool');
          try {
            const createResp = await cognitoClient.send(
              new CreateUserPoolCommand({ PoolName: newPool })
            );
            const arn = createResp.UserPool?.Arn;
            if (!arn) throw new Error('new pool Arn is nil');
            await cognitoClient.send(
              new TagResourceCommand({
                ResourceArn: arn,
                Tags: { Environment: 'test', Owner: 'test-user' },
              })
            );
            const listResp = await cognitoClient.send(
              new ListTagsForResourceCommand({ ResourceArn: arn })
            );
            if (!listResp.Tags) throw new Error('Tags is nil after tagging');
            if (listResp.Tags['Environment'] !== 'test') throw new Error('tag Environment not found');
          } finally {
            try {
              const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
              const pool = listResp.UserPools?.find(p => p.Name === newPool);
              if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
            } catch { /* ignore */ }
          }
        })
      );

      // ListTagsForResource
      results.push(
        await runner.runTest('cognito', 'ListTagsForResource', async () => {
          const newPool = makeUniqueName('TSListTagsPool');
          try {
            const createResp = await cognitoClient.send(
              new CreateUserPoolCommand({ PoolName: newPool })
            );
            const arn = createResp.UserPool?.Arn;
            if (!arn) throw new Error('new pool Arn is nil');
            await cognitoClient.send(
              new TagResourceCommand({
                ResourceArn: arn,
                Tags: { Test: 'value' },
              })
            );
            const listResp = await cognitoClient.send(
              new ListTagsForResourceCommand({ ResourceArn: arn })
            );
            if (!listResp.Tags) throw new Error('Tags is nil');
            if (listResp.Tags['Test'] !== 'value') throw new Error(`expected tag Test=value, got ${listResp.Tags['Test']}`);
          } finally {
            try {
              const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
              const pool = listResp.UserPools?.find(p => p.Name === newPool);
              if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
            } catch { /* ignore */ }
          }
        })
      );

      // UntagResource
      results.push(
        await runner.runTest('cognito', 'UntagResource', async () => {
          const newPool = makeUniqueName('TSUntagPool');
          try {
            const createResp = await cognitoClient.send(
              new CreateUserPoolCommand({ PoolName: newPool })
            );
            const arn = createResp.UserPool?.Arn;
            if (!arn) throw new Error('new pool Arn is nil');
            await cognitoClient.send(
              new TagResourceCommand({
                ResourceArn: arn,
                Tags: { Test: 'value' },
              })
            );
            await cognitoClient.send(
              new UntagResourceCommand({
                ResourceArn: arn,
                TagKeys: ['Test'],
              })
            );
            const listResp = await cognitoClient.send(
              new ListTagsForResourceCommand({ ResourceArn: arn })
            );
            if (listResp.Tags?.['Test']) throw new Error('tag Test should have been removed after UntagResource');
          } finally {
            try {
              const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
              const pool = listResp.UserPools?.find(p => p.Name === newPool);
              if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
            } catch { /* ignore */ }
          }
        })
      );
    }

    // GlobalSignOut
    results.push(
      await runner.runTest('cognito', 'GlobalSignOut', async () => {
        try {
          await cognitoClient.send(
            new GlobalSignOutCommand({ AccessToken: 'dummy-token' })
          );
          throw new Error('expected error for dummy access token');
        } catch (err) {
          if (err instanceof Error && err.message.includes('expected error')) throw err;
        }
      })
    );

  } finally {
    try {
      await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: userPoolId }));
    } catch { /* ignore */ }
  }

  // === ERROR / EDGE CASE TESTS ===

  results.push(
    await runner.runTest('cognito', 'DescribeUserPool_NonExistent', async () => {
      try {
        await cognitoClient.send(
          new DescribeUserPoolCommand({ UserPoolId: 'nonexistent-pool-12345' })
        );
        throw new Error('Expected ResourceNotFoundException but got none');
      } catch (err: unknown) {
        if (err instanceof ResourceNotFoundException) {
        } else if (err instanceof Error && err.name === 'ResourceNotFoundException') {
        } else {
          throw err;
        }
      }
    })
  );

  results.push(
    await runner.runTest('cognito', 'DeleteUserPool_NonExistent', async () => {
      try {
        await cognitoClient.send(
          new DeleteUserPoolCommand({ UserPoolId: 'nonexistent-pool-12345' })
        );
        throw new Error('expected error for non-existent user pool');
      } catch (err) {
        if (err instanceof Error && err.message.includes('expected error')) throw err;
      }
    })
  );

  results.push(
    await runner.runTest('cognito', 'AdminGetUser_NonExistent', async () => {
      try {
        await cognitoClient.send(
          new AdminGetUserCommand({
            UserPoolId: userPoolId,
            Username: 'NonExistentUser_xyz_12345',
          })
        );
        throw new Error('Expected UserNotFoundException but got none');
      } catch (err: unknown) {
        if (err instanceof UserNotFoundException) {
        } else if (err instanceof Error && err.name === 'UserNotFoundException') {
        } else {
          throw err;
        }
      }
    })
  );

  results.push(
    await runner.runTest('cognito', 'AdminGetUser_NonExistentPool', async () => {
      const errPool = makeUniqueName('TSErrPool');
      try {
        const createResp = await cognitoClient.send(
          new CreateUserPoolCommand({ PoolName: errPool })
        );
        try {
          await cognitoClient.send(
            new AdminGetUserCommand({
              UserPoolId: createResp.UserPool?.Id,
              Username: 'nonexistent-user-xyz',
            })
          );
          throw new Error('expected error for non-existent user');
        } catch (err) {
          if (err instanceof Error && err.message.includes('expected error')) throw err;
        }
      } finally {
        try {
          const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
          const pool = listResp.UserPools?.find(p => p.Name === errPool);
          if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
        } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('cognito', 'AdminCreateUser_VerifyAttributes', async () => {
      const attrPool = makeUniqueName('TSAttrPool');
      try {
        const createResp = await cognitoClient.send(
          new CreateUserPoolCommand({ PoolName: attrPool })
        );
        const attrUser = `attr-user-${Date.now()}`;
        const createUserResp = await cognitoClient.send(
          new AdminCreateUserCommand({
            UserPoolId: createResp.UserPool?.Id,
            Username: attrUser,
            TemporaryPassword: 'TempPass123!',
            MessageAction: 'SUPPRESS',
            UserAttributes: [
              { Name: 'email', Value: 'test@example.com' },
              { Name: 'name', Value: 'Test User' },
            ],
          })
        );
        if (!createUserResp.User) throw new Error('user is nil');
        if (createUserResp.User.Username !== attrUser) throw new Error('username mismatch');
      } finally {
        try {
          const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
          const pool = listResp.UserPools?.find(p => p.Name === attrPool);
          if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
        } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('cognito', 'ListUsers_ContainsCreated', async () => {
      const listPool = makeUniqueName('TSListPool');
      try {
        const createResp = await cognitoClient.send(
          new CreateUserPoolCommand({ PoolName: listPool })
        );
        const listUser = `list-user-${Date.now()}`;
        await cognitoClient.send(
          new AdminCreateUserCommand({
            UserPoolId: createResp.UserPool?.Id,
            Username: listUser,
            TemporaryPassword: 'TempPass123!',
            MessageAction: 'SUPPRESS',
          })
        );
        const resp = await cognitoClient.send(
          new ListUsersCommand({ UserPoolId: createResp.UserPool?.Id })
        );
        const found = resp.Users?.some((u) => u.Username === listUser);
        if (!found) throw new Error('created user not found in ListUsers');
      } finally {
        try {
          const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
          const pool = listResp.UserPools?.find(p => p.Name === listPool);
          if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
        } catch { /* ignore */ }
      }
    })
  );

  results.push(
    await runner.runTest('cognito', 'ListGroups_ContainsCreated', async () => {
      const grpPool = makeUniqueName('TSGrpPool');
      try {
        const createResp = await cognitoClient.send(
          new CreateUserPoolCommand({ PoolName: grpPool })
        );
        const testGroup = makeUniqueName('TSGrp');
        await cognitoClient.send(
          new CreateGroupCommand({
            GroupName: testGroup,
            UserPoolId: createResp.UserPool?.Id,
            Description: 'Test group description',
          })
        );
        const resp = await cognitoClient.send(
          new ListGroupsCommand({ UserPoolId: createResp.UserPool?.Id })
        );
        const found = resp.Groups?.some((g) => g.GroupName === testGroup);
        if (!found) throw new Error('created group not found in ListGroups');
      } finally {
        try {
          const listResp = await cognitoClient.send(new ListUserPoolsCommand({ MaxResults: 100 }));
          const pool = listResp.UserPools?.find(p => p.Name === grpPool);
          if (pool?.Id) await cognitoClient.send(new DeleteUserPoolCommand({ UserPoolId: pool.Id }));
        } catch { /* ignore */ }
      }
    })
  );

  return results;
}
