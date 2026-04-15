import {
  CreateIdentityPoolCommand,
  DeleteIdentityPoolCommand,
  GetIdCommand,
  GetOpenIdTokenCommand,
  GetOpenIdTokenForDeveloperIdentityCommand,
  ListIdentityPoolsCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runOpenIdTokenTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];

  results.push(await runner.runTest(svc, 'GetOpenIdToken', async () => {
    const getIdResp = await client.send(new GetIdCommand({ IdentityPoolId: ctx.poolId }));
    if (!getIdResp.IdentityId) throw new Error('IdentityId to be defined');
    const resp = await client.send(new GetOpenIdTokenCommand({
      IdentityId: getIdResp.IdentityId,
    }));
    if (!resp.Token) throw new Error('Token to be defined');
    if (!resp.IdentityId) throw new Error('IdentityId to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetOpenIdTokenForDeveloperIdentity', async () => {
    const devPool = makeUniqueName('cigid-dev');
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: devPool,
        AllowUnauthenticatedIdentities: true,
        DeveloperProviderName: 'dev.example.com',
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      const resp = await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        IdentityId: ctx.identityId || undefined,
        Logins: { 'dev.example.com': `dev-user-${Date.now()}` },
      }));
      if (!resp.IdentityId) throw new Error('IdentityId to be defined');
      if (!resp.Token) throw new Error('Token to be defined');
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === devPool);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'GetOpenIdToken_WithLogins', async () => {
    if (!ctx.identityId) throw new Error('no identityId');
    const resp = await client.send(new GetOpenIdTokenCommand({
      IdentityId: ctx.identityId,
      Logins: { 'graph.facebook.com': 'new-token' },
    }));
    if (!resp.Token) throw new Error('Token to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetOpenIdTokenForDeveloperIdentity_Reuse', async () => {
    const devReusePool = makeUniqueName('cigid-devreuse');
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: devReusePool,
        AllowUnauthenticatedIdentities: true,
        DeveloperProviderName: 'reuse.example.com',
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'reuse.example.com': 'dev-user-reuse-1' },
      }));
      const resp = await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'reuse.example.com': 'dev-user-reuse-1' },
      }));
      if (!resp.IdentityId) throw new Error('IdentityId to be defined');
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === devReusePool);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'GetOpenIdToken_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new GetOpenIdTokenCommand({
        IdentityId: '00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  return results;
}
