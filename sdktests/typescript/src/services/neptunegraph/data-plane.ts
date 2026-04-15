import {
  NeptuneGraphClient,
  ExecuteQueryCommand,
  CancelQueryCommand,
  GetGraphSummaryCommand,
  ListQueriesCommand,
  GetQueryCommand,
  GetGraphCommand,
  CreateGraphUsingImportTaskCommand,
  CancelImportTaskCommand,
  RestoreGraphFromSnapshotCommand,
  DeleteGraphSnapshotCommand,
  GetGraphSnapshotCommand,
  DeleteGraphCommand,
  UpdateGraphCommand,
} from '@aws-sdk/client-neptune-graph';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, safeCleanup, assertErrorContains } from '../../helpers.js';
import type { NeptuneGraphState } from './context.js';

export async function runDataPlaneAndCleanupTests(
  runner: TestRunner,
  client: NeptuneGraphClient,
  state: NeptuneGraphState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const svc = 'neptunegraph';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(svc, name, fn));

  await r('ExecuteQuery_BasicMatch', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new ExecuteQueryCommand({
      graphIdentifier: state.graphID, language: 'OPEN_CYPHER', queryString: 'MATCH (n) RETURN n LIMIT 1',
    }));
    if (!resp.payload) throw new Error('expected payload from ExecuteQuery');
    if (typeof (resp.payload as any).destroy === 'function') (resp.payload as any).destroy();
  });

  await r('CancelQuery_NotImplemented', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    try {
      await client.send(new CancelQueryCommand({ graphIdentifier: state.graphID, queryId: 'q-fake123456' }));
      throw new Error('expected error for CancelQuery (501)');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for CancelQuery (501)') throw err;
      assertErrorContains(err, 'NotImplementedException');
    }
  });

  await r('GetGraphSummary', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new GetGraphSummaryCommand({ graphIdentifier: state.graphID, mode: 'BASIC' }));
    if (!resp.graphSummary) throw new Error('expected non-nil GraphSummary');
  });

  await r('ListQueries', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new ListQueriesCommand({ graphIdentifier: state.graphID, maxResults: 10 }));
    if (!resp.queries) throw new Error('expected non-nil Queries list');
  });

  await r('GetQuery_NotFound', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    try {
      await client.send(new GetQueryCommand({ graphIdentifier: state.graphID, queryId: 'q-nonexist00' }));
      throw new Error('expected error for non-existent query');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for non-existent query') throw err;
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  });

  await r('CreateGraphUsingImportTask', async () => {
    const importGraphName = makeUniqueName('ng-imp');
    const resp = await client.send(new CreateGraphUsingImportTaskCommand({
      graphName: importGraphName,
      source: 's3://test-bucket/import-data/',
      roleArn: 'arn:aws:iam::000000000000:role/NeptuneImportRole',
      format: 'CSV',
    }));
    if (!resp.taskId || resp.taskId === '') throw new Error('expected non-empty task ID');
    await safeCleanup(() => client.send(new CancelImportTaskCommand({ taskIdentifier: resp.taskId! })));
    if (resp.graphId) {
      await safeCleanup(() => client.send(new DeleteGraphCommand({ graphIdentifier: resp.graphId!, skipSnapshot: true })));
    }
  });

  await r('RestoreGraphFromSnapshot', async () => {
    if (!state.snapshotID) throw new Error('no snapshot ID');
    const restoreName = makeUniqueName('ng-restored');
    const resp = await client.send(new RestoreGraphFromSnapshotCommand({
      snapshotIdentifier: state.snapshotID, graphName: restoreName, provisionedMemory: 128, deletionProtection: false,
    }));
    if (!resp.id || resp.id === '') throw new Error('expected non-empty restored graph ID');
    if (!resp.name || resp.name !== restoreName) throw new Error(`expected name=${restoreName}, got ${resp.name}`);
    state.restoredGraphID = resp.id;
  });

  await r('RestoreGraphFromSnapshot_Verify', async () => {
    if (!state.restoredGraphID) throw new Error('no restored graph ID');
    const resp = await client.send(new GetGraphCommand({ graphIdentifier: state.restoredGraphID }));
    if (resp.status !== 'AVAILABLE') throw new Error(`expected status AVAILABLE, got ${resp.status}`);
  });

  if (state.restoredGraphID) {
    await safeCleanup(() => client.send(new DeleteGraphCommand({ graphIdentifier: state.restoredGraphID, skipSnapshot: true })));
  }

  await r('DeleteGraphSnapshot', async () => {
    if (!state.snapshotID) throw new Error('no snapshot ID');
    await client.send(new DeleteGraphSnapshotCommand({ snapshotIdentifier: state.snapshotID }));
  });

  await r('DeleteGraphSnapshot_Verify', async () => {
    if (!state.snapshotID) throw new Error('no snapshot ID');
    try {
      await client.send(new GetGraphSnapshotCommand({ snapshotIdentifier: state.snapshotID }));
      throw new Error('expected error for deleted snapshot');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for deleted snapshot') throw err;
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  });

  await r('GetGraph_NotFound', async () => {
    try {
      await client.send(new GetGraphCommand({ graphIdentifier: 'g-nonexist00' }));
      throw new Error('expected error for non-existent graph');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for non-existent graph') throw err;
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  });

  await r('DeleteGraph_NotFound', async () => {
    try {
      await client.send(new DeleteGraphCommand({ graphIdentifier: 'g-nonexist00', skipSnapshot: true }));
      throw new Error('expected error for non-existent graph');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for non-existent graph') throw err;
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  });

  await r('UpdateGraph_DisableDeletionProtection', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new UpdateGraphCommand({ graphIdentifier: state.graphID, deletionProtection: false }));
  });

  await r('DeleteGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    await client.send(new DeleteGraphCommand({ graphIdentifier: state.graphID, skipSnapshot: true }));
  });

  await r('DeleteGraph_Verify', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    try {
      await client.send(new GetGraphCommand({ graphIdentifier: state.graphID }));
      throw new Error('expected error for deleted graph');
    } catch (err) {
      if (err instanceof Error && err.message === 'expected error for deleted graph') throw err;
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  });

  return results;
}
