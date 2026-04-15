import {
  CreateIdentityPoolCommand,
  DeleteIdentityPoolCommand,
  DescribeIdentityPoolCommand,
  ListIdentityPoolsCommand,
  UpdateIdentityPoolCommand,
} from '@aws-sdk/client-cognito-identity';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';
import type { CognitoIdentityTestContext } from './context.js';

export async function runIdentityPoolTests(ctx: CognitoIdentityTestContext, runner: TestRunner): Promise<TestResult[]> {
  const { client, svc } = ctx;
  const results: TestResult[] = [];
  const poolName = makeUniqueName('cigid-pool');

  results.push(await runner.runTest(svc, 'CreateIdentityPool', async () => {
    const resp = await client.send(new CreateIdentityPoolCommand({
      IdentityPoolName: poolName,
      AllowUnauthenticatedIdentities: true,
    }));
    if (!resp.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
    ctx.poolId = resp.IdentityPoolId;
  }));

  results.push(await runner.runTest(svc, 'CreateIdentityPool_WithOptions', async () => {
    const optName = makeUniqueName('cigid-opt');
    try {
      const resp = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: optName,
        AllowUnauthenticatedIdentities: false,
        AllowClassicFlow: true,
        DeveloperProviderName: 'test-developer.example.com',
      }));
      if (!resp.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      const desc = await client.send(new DescribeIdentityPoolCommand({
        IdentityPoolId: resp.IdentityPoolId,
      }));
      if (!desc.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
      if (desc.AllowClassicFlow !== true) throw new Error('AllowClassicFlow not set');
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === optName);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'DescribeIdentityPool', async () => {
    const resp = await client.send(new DescribeIdentityPoolCommand({ IdentityPoolId: ctx.poolId }));
    if (!resp.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListIdentityPools', async () => {
    const resp = await client.send(new ListIdentityPoolsCommand({ MaxResults: 10 }));
    if (!resp.IdentityPools) throw new Error('IdentityPools to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListIdentityPools_Pagination', async () => {
    const pgPrefix = makeUniqueName('cigid-pg');
    const pgIds: string[] = [];
    try {
      for (const i of [0, 1, 2, 3, 4]) {
        const r = await client.send(new CreateIdentityPoolCommand({
          IdentityPoolName: `${pgPrefix}-${i}`,
          AllowUnauthenticatedIdentities: true,
        }));
        if (r.IdentityPoolId) pgIds.push(r.IdentityPoolId);
      }
      const allIds: string[] = [];
      let token: string | undefined;
      do {
        const resp = await client.send(new ListIdentityPoolsCommand({ MaxResults: 2, NextToken: token }));
        for (const p of resp.IdentityPools ?? []) {
          if (p.IdentityPoolId) allIds.push(p.IdentityPoolId);
        }
        token = resp.NextToken;
      } while (token);
      if (allIds.length < 5) throw new Error(`expected >=5 pools, got ${allIds.length}`);
    } finally {
      for (const id of pgIds) {
        await safeCleanup(() => client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: id })));
      }
    }
  }));

  results.push(await runner.runTest(svc, 'UpdateIdentityPool', async () => {
    const updatedName = poolName + '-updated';
    const resp = await client.send(new UpdateIdentityPoolCommand({
      IdentityPoolId: ctx.poolId,
      IdentityPoolName: updatedName,
      AllowUnauthenticatedIdentities: false,
    }));
    if (!resp.IdentityPoolId) throw new Error('IdentityPoolId to be defined');
    if (resp.IdentityPoolName !== updatedName) throw new Error('Name not updated');
    if (resp.AllowUnauthenticatedIdentities !== false) {
      throw new Error('AllowUnauthenticatedIdentities not updated');
    }
  }));

  results.push(await runner.runTest(svc, 'CreateIdentityPool_WithTags', async () => {
    const tagPoolName = makeUniqueName('cigid-tags');
    try {
      const resp = await client.send(new CreateIdentityPoolCommand({
        IdentityPoolName: tagPoolName,
        AllowUnauthenticatedIdentities: true,
        IdentityPoolTags: { Env: 'production', Cost: 'high' },
      }));
      if (!resp.IdentityPoolTags) throw new Error('IdentityPoolTags to be defined in CreateIdentityPool response');
      if (resp.IdentityPoolTags['Env'] !== 'production') throw new Error('expected Env=production');
    } finally {
      await safeCleanup(async () => {
        const list = await client.send(new ListIdentityPoolsCommand({ MaxResults: 100 }));
        const p = list.IdentityPools?.find(x => x.IdentityPoolName === tagPoolName);
        if (p?.IdentityPoolId) await client.send(new DeleteIdentityPoolCommand({ IdentityPoolId: p.IdentityPoolId }));
      });
    }
  }));

  results.push(await runner.runTest(svc, 'DeleteIdentityPool_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DeleteIdentityPoolCommand({
        IdentityPoolId: 'us-east-1:00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  results.push(await runner.runTest(svc, 'DescribeIdentityPool_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeIdentityPoolCommand({
        IdentityPoolId: 'us-east-1:00000000-0000-0000-0000-000000000000',
      }));
    }, 'ResourceNotFoundException');
  }));

  return results;
}
