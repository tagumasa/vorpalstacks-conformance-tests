import {
  NeptunedataClient as NeptuneDataClient,
  GetEngineStatusCommand,
  ExecuteFastResetCommand,
  GetGremlinQueryStatusCommand,
  ExecuteGremlinQueryCommand,
  GetPropertygraphStatisticsCommand,
  GetPropertygraphSummaryCommand,
  StartLoaderJobCommand,
  GetLoaderJobStatusCommand,
  ListLoaderJobsCommand,
  CancelLoaderJobCommand,
  GetSparqlStatisticsCommand,
  GetRDFGraphSummaryCommand,
  StartMLDataProcessingJobCommand,
  ListGremlinQueriesCommand,
  ListOpenCypherQueriesCommand,
  CancelGremlinQueryCommand,
  ManagePropertygraphStatisticsCommand,
  DeletePropertygraphStatisticsCommand,
  GetPropertygraphStreamCommand,
} from '@aws-sdk/client-neptunedata';
import { TestRunner, TestResult } from '../../runner.js';
import { marshalDoc, createQueryHelpers } from './context.js';

export async function runEngineAndLoaderTests(
  client: NeptuneDataClient,
  runner: TestRunner,
  results: TestResult[],
): Promise<string> {
  const s = 'neptunedata';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));
  const { cypher, fastReset } = createQueryHelpers(client);

  await r('GetEngineStatus', async () => {
    const resp = await client.send(new GetEngineStatusCommand({}));
    if (resp.status !== 'healthy') throw new Error(`expected status=healthy, got ${resp.status}`);
    if (!resp.startTime) throw new Error('expected non-empty startTime');
  });

  await r('GetEngineStatus_GremlinVersion', async () => {
    const resp = await client.send(new GetEngineStatusCommand({}));
    if (!resp.gremlin?.version) throw new Error('expected non-empty gremlin version');
  });

  await r('GetEngineStatus_OpenCypherVersion', async () => {
    const resp = await client.send(new GetEngineStatusCommand({}));
    if (!resp.opencypher?.version) throw new Error('expected non-empty opencypher version');
  });

  await r('GetEngineStatus_Role', async () => {
    const resp = await client.send(new GetEngineStatusCommand({}));
    if (resp.role !== 'writer' && resp.role !== 'reader') {
      throw new Error(`expected role=writer|reader, got ${resp.role}`);
    }
  });

  await r('ExecuteFastReset_Initiate', async () => {
    const resp = await client.send(
      new ExecuteFastResetCommand({ action: 'initiateDatabaseReset' }),
    );
    if (!resp.payload?.token) throw new Error('expected non-empty fast reset token');
  });

  await r('ExecuteFastReset_Perform', async () => {
    const initResp = await client.send(
      new ExecuteFastResetCommand({ action: 'initiateDatabaseReset' }),
    );
    const token = initResp.payload?.token;
    if (!token) throw new Error('expected non-empty token from initiateDatabaseReset');
    const performResp = await client.send(
      new ExecuteFastResetCommand({ action: 'performDatabaseReset', token }),
    );
    if (!performResp.status) throw new Error('expected non-empty status from performDatabaseReset');
  });

  let loaderJobID = '';

  await r('StartLoaderJob', async () => {
    const resp = await client.send(
      new StartLoaderJobCommand({
        source: 's3://test-bucket/data',
        format: 'csv',
        iamRoleArn: 'arn:aws:iam::000000000000:role/NeptuneLoadRole',
        s3BucketRegion: 'us-east-1',
      }),
    );
    if (!resp.payload) throw new Error('expected non-nil loader job payload');
    const data = marshalDoc(resp.payload);
    const payloadMap = JSON.parse(data) as Record<string, unknown>;
    if (typeof payloadMap['loadId'] === 'string' && payloadMap['loadId'] !== '') {
      loaderJobID = payloadMap['loadId'];
    } else {
      throw new Error(`expected loadId in payload, got ${data}`);
    }
  });

  await r('GetLoaderJobStatus', async () => {
    if (!loaderJobID) throw new Error('no loader job ID from StartLoaderJob');
    const resp = await client.send(
      new GetLoaderJobStatusCommand({ loadId: loaderJobID }),
    );
    if (!resp.payload) throw new Error('expected non-nil loader job status payload');
  });

  await r('ListLoaderJobs', async () => {
    const resp = await client.send(new ListLoaderJobsCommand({}));
    if (!resp.payload) throw new Error('expected non-nil list loader jobs payload');
  });

  await r('CancelLoaderJob', async () => {
    if (!loaderJobID) throw new Error('no loader job ID from StartLoaderJob');
    await client.send(new CancelLoaderJobCommand({ loadId: loaderJobID }));
  });

  await r('GetSparqlStatistics_Unsupported', async () => {
    try {
      await client.send(new GetSparqlStatisticsCommand({}));
      throw new Error('expected error for unsupported SPARQL statistics');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for unsupported SPARQL statistics') throw err;
    }
  });

  await r('GetRDFGraphSummary_Unsupported', async () => {
    try {
      await client.send(new GetRDFGraphSummaryCommand({}));
      throw new Error('expected error for unsupported RDF graph summary');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for unsupported RDF graph summary') throw err;
    }
  });

  await r('StartMLDataProcessingJob_Unsupported', async () => {
    try {
      await client.send(
        new StartMLDataProcessingJobCommand({
          inputDataS3Location: 's3://test/ml-input',
          processedDataS3Location: 's3://test/ml-output',
        }),
      );
      throw new Error('expected error for unsupported ML data processing job');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for unsupported ML data processing job') throw err;
    }
  });

  await r('ListGremlinQueries', async () => {
    await client.send(new ListGremlinQueriesCommand({}));
  });

  await r('ListOpenCypherQueries', async () => {
    await client.send(new ListOpenCypherQueriesCommand({}));
  });

  await r('GetGremlinQueryStatus', async () => {
    const resp = await client.send(
      new ExecuteGremlinQueryCommand({ gremlinQuery: 'g.V().count()' }),
    );
    if (!resp.requestId) throw new Error('expected requestId from gremlin query');
    const statusResp = await client.send(
      new GetGremlinQueryStatusCommand({ queryId: resp.requestId }),
    );
    if (statusResp.queryId !== resp.requestId) {
      throw new Error(`queryId mismatch: expected ${resp.requestId}, got ${statusResp.queryId}`);
    }
  });

  await r('GetOpenCypherQueryStatus', async () => {
    await cypher('MATCH (n) RETURN count(n)');
  });

  await r('GetPropertygraphStatistics', async () => {
    const resp = await client.send(new GetPropertygraphStatisticsCommand({}));
    if (!resp.status) throw new Error('expected non-nil status');
  });

  await r('GetPropertygraphSummary', async () => {
    const resp = await client.send(
      new GetPropertygraphSummaryCommand({ mode: 'basic' }),
    );
    if (!resp.statusCode) throw new Error('expected non-nil statusCode');
  });

  await r('CancelGremlinQuery', async () => {
    try {
      await client.send(new CancelGremlinQueryCommand({ queryId: 'nonexistent-query-id' }));
      throw new Error('expected error for cancelling non-existent query');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for cancelling non-existent query') throw err;
    }
  });

  await r('CancelOpenCypherQuery', async () => {
    await cypher('MATCH (n) RETURN count(n)');
    await client.send(new ListOpenCypherQueriesCommand({}));
  });

  await r('ManagePropertygraphStatistics_Disable', async () => {
    await client.send(
      new ManagePropertygraphStatisticsCommand({ mode: 'disableAutoCompute' }),
    );
  });

  await r('ManagePropertygraphStatistics_Enable', async () => {
    await client.send(
      new ManagePropertygraphStatisticsCommand({ mode: 'enableAutoCompute' }),
    );
  });

  await r('ManagePropertygraphStatistics_Refresh', async () => {
    const resp = await client.send(
      new ManagePropertygraphStatisticsCommand({ mode: 'refresh' }),
    );
    if (!resp.status) throw new Error('expected non-nil status from refresh');
  });

  await r('DeletePropertygraphStatistics', async () => {
    const resp = await client.send(new DeletePropertygraphStatisticsCommand({}));
    if (!resp.status) throw new Error('expected non-nil status from delete statistics');
  });

  await r('GetPropertygraphStream', async () => {
    const resp = await client.send(new GetPropertygraphStreamCommand({}));
    if (!resp.format) throw new Error('expected non-nil format from stream');
  });

  await r('GetPropertygraphSummary_Detailed', async () => {
    await client.send(
      new GetPropertygraphSummaryCommand({ mode: 'detailed' }),
    );
  });

  return loaderJobID;
}
