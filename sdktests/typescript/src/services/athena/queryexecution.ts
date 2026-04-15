import {
  StartQueryExecutionCommand,
  GetQueryExecutionCommand,
  ListQueryExecutionsCommand,
  GetQueryResultsCommand,
  StopQueryExecutionCommand,
  GetQueryRuntimeStatisticsCommand,
  BatchGetQueryExecutionCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import type { AthenaTestContext } from './context.js';

export async function runQueryExecutionTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'StartQueryExecution', async () => {
    const resp = await client.send(new StartQueryExecutionCommand({
      QueryString: 'SELECT 1',
      QueryExecutionContext: { Database: 'default' },
      ResultConfiguration: { OutputLocation: 's3://test-bucket/athena/' },
    }));
    if (!resp.QueryExecutionId) throw new Error('QueryExecutionId to be defined');
    ctx.queryExecutionId = resp.QueryExecutionId;
  }));

  results.push(await runner.runTest(svc, 'GetQueryExecution', async () => {
    const resp = await client.send(new GetQueryExecutionCommand({ QueryExecutionId: ctx.queryExecutionId }));
    if (!resp.QueryExecution) throw new Error('QueryExecution to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListQueryExecutions', async () => {
    const resp = await client.send(new ListQueryExecutionsCommand({ MaxResults: 10 }));
    if (!resp.QueryExecutionIds) throw new Error('QueryExecutionIds to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetQueryResults', async () => {
    const resp = await client.send(new GetQueryResultsCommand({ QueryExecutionId: ctx.queryExecutionId }));
    if (!resp.ResultSet) throw new Error('result set to be defined');
  }));

  results.push(await runner.runTest(svc, 'StopQueryExecution', async () => {
    const getResp = await client.send(new GetQueryExecutionCommand({ QueryExecutionId: ctx.queryExecutionId }));
    const state = getResp.QueryExecution?.Status?.State;
    if (state === 'QUEUED' || state === 'RUNNING') {
      await client.send(new StopQueryExecutionCommand({ QueryExecutionId: ctx.queryExecutionId }));
    }
  }));

  return results;
}

export async function runQueryExecutionFinallyTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  let resultsQueryId = '';
  results.push(await runner.runTest(svc, 'GetQueryResults_StartQuery', async () => {
    const resp = await client.send(new StartQueryExecutionCommand({
      QueryString: 'SHOW DATABASES',
      QueryExecutionContext: { Database: 'default' },
    }));
    resultsQueryId = resp.QueryExecutionId || '';
    if (!resultsQueryId) throw new Error('QueryExecutionId to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetQueryResults_WaitForCompletion', async () => {
    for (const i of [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29]) {
      const resp = await client.send(new GetQueryExecutionCommand({ QueryExecutionId: resultsQueryId }));
      const state = resp.QueryExecution?.Status?.State;
      if (state === 'SUCCEEDED') return;
      if (state === 'FAILED' || state === 'CANCELLED') throw new Error(`query ended in state ${state}`);
      await new Promise(r => setTimeout(r, 500));
    }
    throw new Error('query did not complete within timeout');
  }));

  results.push(await runner.runTest(svc, 'GetQueryRuntimeStatistics', async () => {
    const resp = await client.send(new GetQueryRuntimeStatisticsCommand({ QueryExecutionId: resultsQueryId }));
    if (!resp.QueryRuntimeStatistics) throw new Error('query runtime statistics to be defined');
  }));

  let batchQEId1 = '';
  let batchQEId2 = '';

  results.push(await runner.runTest(svc, 'BatchGetQueryExecution_Setup', async () => {
    const resp1 = await client.send(new StartQueryExecutionCommand({
      QueryString: 'SELECT 1',
      QueryExecutionContext: { Database: 'default' },
    }));
    if (!resp1.QueryExecutionId) throw new Error('QueryExecutionId to be defined');
    batchQEId1 = resp1.QueryExecutionId;
    const resp2 = await client.send(new StartQueryExecutionCommand({
      QueryString: 'SELECT 2',
      QueryExecutionContext: { Database: 'default' },
    }));
    if (!resp2.QueryExecutionId) throw new Error('QueryExecutionId to be defined');
    batchQEId2 = resp2.QueryExecutionId;
    for (const i of [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29]) {
      const r1 = await client.send(new GetQueryExecutionCommand({ QueryExecutionId: batchQEId1 }));
      const r2 = await client.send(new GetQueryExecutionCommand({ QueryExecutionId: batchQEId2 }));
      if (r1.QueryExecution?.Status?.State === 'SUCCEEDED' && r2.QueryExecution?.Status?.State === 'SUCCEEDED') return;
      await new Promise(r => setTimeout(r, 500));
    }
    throw new Error('queries did not complete within timeout');
  }));

  results.push(await runner.runTest(svc, 'BatchGetQueryExecution', async () => {
    const resp = await client.send(new BatchGetQueryExecutionCommand({
      QueryExecutionIds: [batchQEId1, batchQEId2, 'nonexistent-qe-id'],
    }));
    if (!resp.QueryExecutions || resp.QueryExecutions.length !== 2) {
      throw new Error(`expected 2 query executions, got ${resp.QueryExecutions?.length}`);
    }
    if (!resp.UnprocessedQueryExecutionIds || resp.UnprocessedQueryExecutionIds.length !== 1) {
      throw new Error(`expected 1 unprocessed, got ${resp.UnprocessedQueryExecutionIds?.length}`);
    }
  }));

  return results;
}
