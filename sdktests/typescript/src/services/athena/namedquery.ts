import {
  CreateNamedQueryCommand,
  GetNamedQueryCommand,
  UpdateNamedQueryCommand,
  DeleteNamedQueryCommand,
  BatchGetNamedQueryCommand,
  ListNamedQueriesCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';
import type { AthenaTestContext } from './context.js';

export async function runNamedQueryTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'CreateNamedQuery', async () => {
    const resp = await client.send(new CreateNamedQueryCommand({
      Name: ctx.nqName,
      Database: 'default',
      QueryString: 'SELECT 1',
      Description: 'Test query',
    }));
    if (!resp.NamedQueryId) throw new Error('NamedQueryId to be defined');
    ctx.nqId = resp.NamedQueryId;
  }));

  results.push(await runner.runTest(svc, 'GetNamedQuery', async () => {
    const resp = await client.send(new GetNamedQueryCommand({ NamedQueryId: ctx.nqId }));
    if (!resp.NamedQuery) throw new Error('NamedQuery to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListNamedQueries', async () => {
    const resp = await client.send(new ListNamedQueriesCommand({ MaxResults: 10 }));
    if (!resp.NamedQueryIds) throw new Error('NamedQueryIds to be defined');
  }));

  results.push(await runner.runTest(svc, 'BatchGetNamedQuery', async () => {
    const resp = await client.send(new BatchGetNamedQueryCommand({
      NamedQueryIds: [ctx.nqId],
    }));
    if (!resp.NamedQueries || resp.NamedQueries.length === 0) throw new Error('NamedQueries to be non-empty');
    if (resp.NamedQueries[0].NamedQueryId !== ctx.nqId) throw new Error('NamedQueryId mismatch');
  }));

  results.push(await runner.runTest(svc, 'UpdateNamedQuery', async () => {
    const resp = await client.send(new UpdateNamedQueryCommand({
      NamedQueryId: ctx.nqId,
      Name: ctx.updatedNqName,
      Description: 'Updated test query',
      QueryString: 'SELECT 2',
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetNamedQuery_AfterUpdate', async () => {
    const resp = await client.send(new GetNamedQueryCommand({ NamedQueryId: ctx.nqId }));
    if (!resp.NamedQuery) throw new Error('NamedQuery to be defined');
    if (resp.NamedQuery.Name !== ctx.updatedNqName) {
      throw new Error(`Expected name ${ctx.updatedNqName}, got ${resp.NamedQuery.Name}`);
    }
    if (resp.NamedQuery.QueryString !== 'SELECT 2') {
      throw new Error(`Expected query 'SELECT 2', got ${resp.NamedQuery.QueryString}`);
    }
  }));

  results.push(await runner.runTest(svc, 'UpdateNamedQuery_OldNameReusable', async () => {
    const createResp = await client.send(new CreateNamedQueryCommand({
      Name: ctx.oldNameReusable,
      Database: 'default',
      QueryString: 'SELECT 3',
    }));
    if (!createResp.NamedQueryId) throw new Error('NamedQueryId to be defined');
    ctx.reusableNqId = createResp.NamedQueryId;

    const renamedName = makeUniqueName('renamedquery');
    await client.send(new UpdateNamedQueryCommand({
      NamedQueryId: ctx.reusableNqId,
      Name: renamedName,
      Description: 'Renamed',
      QueryString: 'SELECT 4',
    }));

    const newResp = await client.send(new CreateNamedQueryCommand({
      Name: ctx.oldNameReusable,
      Database: 'default',
      QueryString: 'SELECT 5',
    }));
    if (!newResp.NamedQueryId) throw new Error('Should be able to reuse old name');
  }));

  results.push(await runner.runTest(svc, 'UpdateNamedQuery_NewNameNotReusable', async () => {
    try {
      await client.send(new CreateNamedQueryCommand({
        Name: ctx.updatedNqName,
        Database: 'default',
        QueryString: 'SELECT duplicate',
      }));
      throw new Error('expected an error');
    } catch (err: unknown) {
      const name = err instanceof Error ? err.name : '';
      if (name !== 'InvalidRequestException' && name !== 'ResourceAlreadyExistsException') {
        throw new Error(`Expected InvalidRequestException or ResourceAlreadyExistsException, got ${name}`);
      }
    }
  }));

  results.push(await runner.runTest(svc, 'DeleteNamedQuery', async () => {
    const resp = await client.send(new DeleteNamedQueryCommand({ NamedQueryId: ctx.nqId }));
    if (!resp) throw new Error('response to be defined');
  }));

  return results;
}

export async function runNamedQueryFinallyTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'GetNamedQuery_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new GetNamedQueryCommand({ NamedQueryId: '00000000-0000-0000-0000-000000000000' }));
    }, 'ResourceNotFoundException');
  }));

  const bnq1 = makeUniqueName('athena-bnq1');
  const bnq2 = makeUniqueName('athena-bnq2');
  results.push(await runner.runTest(svc, 'BatchGetNamedQuery_Setup', async () => {
    try {
      await client.send(new CreateNamedQueryCommand({ Name: bnq1, Database: 'default', QueryString: 'SELECT 1' }));
      await client.send(new CreateNamedQueryCommand({ Name: bnq2, Database: 'default', QueryString: 'SELECT 2' }));
    } finally {
      await safeCleanup(() => client.send(new DeleteNamedQueryCommand({ NamedQueryId: bnq1 })));
      await safeCleanup(() => client.send(new DeleteNamedQueryCommand({ NamedQueryId: bnq2 })));
    }
  }));

  return results;
}
