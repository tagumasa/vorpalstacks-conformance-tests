import {
  CreateIdentityPoolCommand,
  DeleteIdentityPoolCommand,
  GetIdentityPoolRolesCommand,
  SetIdentityPoolRolesCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runRolesTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];
  const authRole = `arn:aws:iam::000000000000:role/auth-${Date.now()}`;
  const unauthRole = `arn:aws:iam::000000000000:role/unauth-${Date.now()}`;

  results.push(await runner.runTest(svc, 'SetIdentityPoolRoles', async () => {
    await client.send(new SetIdentityPoolRolesCommand({
      IdentityPoolId: ctx.poolId,
      Roles: {
        authenticated: authRole,
        unauthenticated: unauthRole,
      },
    }));
  }));

  results.push(await runner.runTest(svc, 'GetIdentityPoolRoles', async () => {
    const resp = await client.send(new GetIdentityPoolRolesCommand({ IdentityPoolId: ctx.poolId }));
    if (!resp.Roles) throw new Error('Roles to be defined');
  }));

  results.push(await runner.runTest(svc, 'SetIdentityPoolRoles_WithMappings', async () => {
    const resp = await client.send(new SetIdentityPoolRolesCommand({
      IdentityPoolId: ctx.poolId,
      Roles: {
        authenticated: authRole,
        unauthenticated: unauthRole,
      },
      RoleMappings: {
        'graph.facebook.com': {
          Type: 'Token',
          AmbiguousRoleResolution: 'AuthenticatedRole',
        },
      },
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest(svc, 'SetIdentityPoolRoles_RuleMappings', async () => {
    const rulePoolName = makeUniqueName('cog-idpool-rules');
    let rulePoolId = '';
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: rulePoolName,
        AllowUnauthenticatedIdentities: true,
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      rulePoolId = cr.IdentityPoolId;
      await client.send(new SetIdentityPoolRolesCommand({
        IdentityPoolId: rulePoolId,
        Roles: {
          authenticated: 'arn:aws:iam::123456789012:role/auth',
        },
        RoleMappings: {
          'graph.facebook.com': {
            Type: 'Rules',
            AmbiguousRoleResolution: 'Deny',
            RulesConfiguration: {
              Rules: [{
                Claim: 'isAdmin',
                MatchType: 'Equals',
                Value: 'true',
                RoleARN: 'arn:aws:iam::123456789012:role/admin',
              }],
            },
          },
        },
      }));
      const resp = await client.send(new GetIdentityPoolRolesCommand({
        IdentityPoolId: rulePoolId,
      }));
      if (!resp.RoleMappings) throw new Error('RoleMappings to be defined');
      const m = resp.RoleMappings['graph.facebook.com'];
      if (m?.Type !== 'Rules') throw new Error('expected Rules type');
    } finally {
      await safeCleanup(() => client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: rulePoolId })));
    }
  }));

  results.push(await runner.runTest(svc, 'GetIdentityPoolRoles_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new GetIdentityPoolRolesCommand({
        IdentityPoolId: 'us-east-1:00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  return results;
}
