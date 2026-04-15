import {
  CognitoIdentityClient,
  DeleteIdentitiesCommand,
  DescribeIdentityCommand,
  GetCredentialsForIdentityCommand,
  GetIdCommand,
  ListIdentitiesCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import { assertThrows } from '../../helpers.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runIdentityTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];

  results.push(await runner.runTest(svc, 'GetId', async () => {
    const resp = await client.send(new GetIdCommand({
      IdentityPoolId: ctx.poolId,
    }));
    if (!resp.IdentityId) throw new Error('IdentityId to be defined');
    ctx.identityId = resp.IdentityId;
  }));

  results.push(await runner.runTest(svc, 'DescribeIdentity', async () => {
    if (!ctx.identityId) throw new Error('no identityId');
    const resp = await client.send(new DescribeIdentityCommand({ IdentityId: ctx.identityId }));
    if (!resp.IdentityId) throw new Error('IdentityId to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetCredentialsForIdentity', async () => {
    if (!ctx.identityId) throw new Error('no identityId');
    await client.send(new GetCredentialsForIdentityCommand({ IdentityId: ctx.identityId }));
  }));

  results.push(await runner.runTest(svc, 'ListIdentities', async () => {
    const resp = await client.send(new ListIdentitiesCommand({
      IdentityPoolId: ctx.poolId,
      MaxResults: 10,
    }));
    if (!resp.Identities) throw new Error('Identities to be defined');
  }));

  results.push(await runner.runTest(svc, 'DeleteIdentities', async () => {
    const getIdResp = await client.send(new GetIdCommand({ IdentityPoolId: ctx.poolId }));
    if (!getIdResp.IdentityId) throw new Error('IdentityId to be defined');
    await client.send(new DeleteIdentitiesCommand({
      IdentityIdsToDelete: [getIdResp.IdentityId],
    }));
  }));

  results.push(await runner.runTest(svc, 'GetId_NonExistentPool', async () => {
    await assertThrows(async () => {
      await client.send(new GetIdCommand({
        IdentityPoolId: 'us-east-1:00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest(svc, 'GetId_WithLogins', async () => {
    const resp = await client.send(new GetIdCommand({
      IdentityPoolId: ctx.poolId,
      Logins: { 'graph.facebook.com': 'test-token' },
    }));
    if (!resp.IdentityId) throw new Error('IdentityId to be defined');
    ctx.identityId = resp.IdentityId;
  }));

  results.push(await runner.runTest(svc, 'GetId_SecondIdentity', async () => {
    const resp = await client.send(new GetIdCommand({
      IdentityPoolId: ctx.poolId,
      Logins: { 'accounts.google.com': 'google-token' },
    }));
    if (!resp.IdentityId) throw new Error('IdentityId to be defined');
  }));

  results.push(await runner.runTest(svc, 'DescribeIdentity_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeIdentityCommand({
        IdentityId: '00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest(svc, 'GetCredentialsForIdentity_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new GetCredentialsForIdentityCommand({
        IdentityId: '00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest(svc, 'DeleteIdentities_NonExistent', async () => {
    const resp = await client.send(new DeleteIdentitiesCommand({
      IdentityIdsToDelete: ['00000000-0000-0000-0000-000000000000'],
    }));
    if (!resp.UnprocessedIdentityIds || resp.UnprocessedIdentityIds.length !== 1) {
      throw new Error('expected 1 unprocessed identity');
    }
  }));

  return results;
}
