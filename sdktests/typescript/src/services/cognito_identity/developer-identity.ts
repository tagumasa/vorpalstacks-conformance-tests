import {
  CreateIdentityPoolCommand,
  DeleteIdentityPoolCommand,
  GetIdCommand,
  GetOpenIdTokenForDeveloperIdentityCommand,
  ListIdentityPoolsCommand,
  LookupDeveloperIdentityCommand,
  MergeDeveloperIdentitiesCommand,
  UnlinkDeveloperIdentityCommand,
  UnlinkIdentityCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup } from '../../helpers.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runDeveloperIdentityTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];

  results.push(await runner.runTest(svc, 'LookupDeveloperIdentity', async () => {
    const devPool2 = makeUniqueName('cigid-lkup');
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: devPool2,
        AllowUnauthenticatedIdentities: true,
        DeveloperProviderName: 'lookup.example.com',
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      const devUserId = `dev-user-${Date.now()}`;
      await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'lookup.example.com': devUserId },
      }));
      const resp = await client.send(new LookupDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        DeveloperUserIdentifier: devUserId,
        MaxResults: 10,
      } as any));
      if (!resp.DeveloperUserIdentifierList?.length) throw new Error('expected at least 1 developer user identifier');
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === devPool2);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'MergeDeveloperIdentities', async () => {
    const devPool3 = makeUniqueName('cigid-merge');
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: devPool3,
        AllowUnauthenticatedIdentities: true,
        DeveloperProviderName: 'merge.example.com',
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      const sourceUser = `src-user-${Date.now()}`;
      const destUser = `dst-user-${Date.now()}`;
      const srcResp = await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'merge.example.com': sourceUser },
      }));
      const dstResp = await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'merge.example.com': destUser },
      }));
      if (!srcResp.IdentityId || !dstResp.IdentityId) throw new Error('IdentityId to be defined');
      const resp = await client.send(new MergeDeveloperIdentitiesCommand({
        SourceUserIdentifier: sourceUser,
        DestinationUserIdentifier: destUser,
        DeveloperProviderName: 'merge.example.com',
        IdentityPoolId: cr.IdentityPoolId,
      }));
      if (!resp.IdentityId) throw new Error('IdentityId to be defined');
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === devPool3);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'UnlinkDeveloperIdentity', async () => {
    const devPool4 = makeUniqueName('cigid-unlink');
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: devPool4,
        AllowUnauthenticatedIdentities: true,
        DeveloperProviderName: 'unlink.example.com',
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      const devUserId = `unlink-user-${Date.now()}`;
      const tokResp = await client.send(new GetOpenIdTokenForDeveloperIdentityCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'unlink.example.com': devUserId },
      }));
      if (!tokResp.IdentityId) throw new Error('IdentityId to be defined');
      await client.send(new UnlinkDeveloperIdentityCommand({
        IdentityId: tokResp.IdentityId,
        IdentityPoolId: cr.IdentityPoolId,
        DeveloperProviderName: 'unlink.example.com',
        DeveloperUserIdentifier: devUserId,
      }));
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === devPool4);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'UnlinkIdentity', async () => {
    const unlinkPool = makeUniqueName('cigid-unlinkid');
    try {
      const cr = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: unlinkPool,
        AllowUnauthenticatedIdentities: true,
        CognitoIdentityProviders: [
          {
            ProviderName: 'cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool',
            ClientId: 'test-client-id',
          },
        ],
      }));
      if (!cr.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      const getIdResp = await client.send(new GetIdCommand({
        IdentityPoolId: cr.IdentityPoolId,
        Logins: { 'cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool': 'test-token' },
      }));
      if (!getIdResp.IdentityId) throw new Error('IdentityId to be defined');
      await client.send(new UnlinkIdentityCommand({
        IdentityId: getIdResp.IdentityId,
        Logins: { 'cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool': 'test-token' },
        LoginsToRemove: ['cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool'],
      }));
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === unlinkPool);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  return results;
}
