import {
  GetPrincipalTagAttributeMapCommand,
  SetPrincipalTagAttributeMapCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runPrincipalTagsTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];

  results.push(await runner.runTest(svc, 'SetPrincipalTagAttributeMap', async () => {
    await client.send(new SetPrincipalTagAttributeMapCommand({
      IdentityPoolId: ctx.poolId,
      IdentityProviderName: 'graph.facebook.com',
      UseDefaults: false,
      PrincipalTags: { email: 'email', name: 'displayName' },
    }));
  }));

  results.push(await runner.runTest(svc, 'GetPrincipalTagAttributeMap', async () => {
    const resp = await client.send(new GetPrincipalTagAttributeMapCommand({
      IdentityPoolId: ctx.poolId,
      IdentityProviderName: 'graph.facebook.com',
    }));
    if (!resp.PrincipalTags) throw new Error('PrincipalTags to be defined');
    if (resp.UseDefaults !== false) throw new Error('UseDefaults should be false');
  }));

  results.push(await runner.runTest(svc, 'GetPrincipalTagAttributeMap_Defaults', async () => {
    const resp = await client.send(new GetPrincipalTagAttributeMapCommand({
      IdentityPoolId: ctx.poolId,
      IdentityProviderName: 'accounts.google.com',
    }));
    if (resp.UseDefaults !== true) throw new Error('expected UseDefaults=true for non-existent mapping');
  }));

  return results;
}
