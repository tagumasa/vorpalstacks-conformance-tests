import {
  NeptuneGraphClient,
  StartImportTaskCommand,
  GetImportTaskCommand,
  ListImportTasksCommand,
  CancelImportTaskCommand,
  StartExportTaskCommand,
  GetExportTaskCommand,
  ListExportTasksCommand,
  CancelExportTaskCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
  UntagResourceCommand,
} from '@aws-sdk/client-neptune-graph';
import type { TestRunner, TestResult } from '../../runner.js';
import type { NeptuneGraphState } from './context.js';

export async function runTaskAndTagTests(
  runner: TestRunner,
  client: NeptuneGraphClient,
  state: NeptuneGraphState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const svc = 'neptunegraph';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(svc, name, fn));

  await r('StartImportTask', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new StartImportTaskCommand({
      graphIdentifier: state.graphID,
      source: 's3://test-bucket/import-data/',
      roleArn: 'arn:aws:iam::000000000000:role/NeptuneImportRole',
      format: 'CSV',
    }));
    if (!resp.taskId || resp.taskId === '') throw new Error('expected non-empty import task ID');
    if (resp.status !== 'INITIALIZING' && resp.status !== 'IMPORTING') {
      throw new Error(`expected INITIALIZING/IMPORTING status, got ${resp.status}`);
    }
    state.importTaskID = resp.taskId;
  });

  await r('GetImportTask', async () => {
    if (!state.importTaskID) throw new Error('no import task ID');
    const resp = await client.send(new GetImportTaskCommand({ taskIdentifier: state.importTaskID }));
    if (!resp.taskId || resp.taskId !== state.importTaskID) throw new Error(`expected taskId=${state.importTaskID}, got ${resp.taskId}`);
  });

  await r('ListImportTasks', async () => {
    const resp = await client.send(new ListImportTasksCommand({}));
    if (!resp.tasks) throw new Error('expected non-nil Tasks list');
    const found = resp.tasks.some((t) => t.taskId === state.importTaskID);
    if (!found) throw new Error('import task not found in ListImportTasks');
  });

  await r('CancelImportTask', async () => {
    if (!state.importTaskID) throw new Error('no import task ID');
    await client.send(new CancelImportTaskCommand({ taskIdentifier: state.importTaskID }));
  });

  await r('StartExportTask', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new StartExportTaskCommand({
      graphIdentifier: state.graphID,
      destination: 's3://test-bucket/export-data/',
      kmsKeyIdentifier: 'arn:aws:kms:us-east-1:000000000000:key/12345678-1234-1234-1234-123456789012',
      roleArn: 'arn:aws:iam::000000000000:role/NeptuneExportRole',
      format: 'CSV',
    }));
    if (!resp.taskId || resp.taskId === '') throw new Error('expected non-empty export task ID');
    state.exportTaskID = resp.taskId;
  });

  await r('GetExportTask', async () => {
    if (!state.exportTaskID) throw new Error('no export task ID');
    const resp = await client.send(new GetExportTaskCommand({ taskIdentifier: state.exportTaskID }));
    if (!resp.taskId || resp.taskId !== state.exportTaskID) throw new Error(`expected taskId=${state.exportTaskID}, got ${resp.taskId}`);
  });

  await r('ListExportTasks', async () => {
    const resp = await client.send(new ListExportTasksCommand({}));
    if (!resp.tasks) throw new Error('expected non-nil Tasks list');
    const found = resp.tasks.some((t) => t.taskId === state.exportTaskID);
    if (!found) throw new Error('export task not found in ListExportTasks');
  });

  await r('ListExportTasks_FilterByGraph', async () => {
    if (!state.graphID) throw new Error('no graph ID');
    const resp = await client.send(new ListExportTasksCommand({ graphIdentifier: state.graphID }));
    if (!resp.tasks) throw new Error('expected non-nil Tasks list');
    const found = resp.tasks.some((t) => t.taskId === state.exportTaskID);
    if (!found) throw new Error('export task not found when filtering by graph');
  });

  await r('CancelExportTask', async () => {
    if (!state.exportTaskID) throw new Error('no export task ID');
    await client.send(new CancelExportTaskCommand({ taskIdentifier: state.exportTaskID }));
  });

  await r('TagResource', async () => {
    if (!state.graphARN) throw new Error('no graph ARN');
    await client.send(new TagResourceCommand({
      resourceArn: state.graphARN,
      tags: { ExtraTag: 'extra-value', CreatedBy: 'sdk-tests' },
    }));
  });

  await r('ListTagsForResource', async () => {
    if (!state.graphARN) throw new Error('no graph ARN');
    const resp = await client.send(new ListTagsForResourceCommand({ resourceArn: state.graphARN }));
    if (!resp.tags) throw new Error('expected non-nil Tags map');
    if (resp.tags['Environment'] !== 'test') throw new Error(`expected tag Environment=test, got ${resp.tags['Environment']}`);
    if (resp.tags['Owner'] !== 'sdk-test') throw new Error(`expected tag Owner=sdk-test, got ${resp.tags['Owner']}`);
    if (resp.tags['ExtraTag'] !== 'extra-value') throw new Error(`expected tag ExtraTag=extra-value, got ${resp.tags['ExtraTag']}`);
  });

  await r('UntagResource', async () => {
    if (!state.graphARN) throw new Error('no graph ARN');
    await client.send(new UntagResourceCommand({ resourceArn: state.graphARN, tagKeys: ['ExtraTag'] }));
  });

  await r('ListTagsForResource_AfterUntag', async () => {
    if (!state.graphARN) throw new Error('no graph ARN');
    const resp = await client.send(new ListTagsForResourceCommand({ resourceArn: state.graphARN }));
    if (!resp.tags) throw new Error('expected non-nil Tags map');
    if ('ExtraTag' in resp.tags) throw new Error('expected ExtraTag to be removed');
    if (resp.tags['Environment'] !== 'test') throw new Error(`expected tag Environment=test still present`);
  });

  return results;
}
