import {
  TimestreamWriteClient,
  CreateDatabaseCommand,
  DescribeDatabaseCommand,
  DeleteDatabaseCommand,
  CreateBatchLoadTaskCommand,
  DescribeBatchLoadTaskCommand,
  ListBatchLoadTasksCommand,
  DeleteTableCommand,
  CreateTableCommand,
  TagResourceCommand,
  UntagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-timestream-write';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export async function runTagsAndBatchTests(
  runner: TestRunner,
  client: TimestreamWriteClient,
  region: string,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const s = 'timestream';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  const tagDb = makeUniqueName('tag-db');
  await r('CreateDatabase_WithTags', async () => {
    await client.send(new CreateDatabaseCommand({
      DatabaseName: tagDb,
      Tags: [{ Key: 'env', Value: 'test' }, { Key: 'team', Value: 'dev' }],
    }));
  });

  await r('DescribeDatabase_Tags', async () => {
    const resp = await client.send(new DescribeDatabaseCommand({ DatabaseName: tagDb }));
    if (!resp.Database) throw new Error('Database is null');
  });

  await r('TagResource_UntagResource_ListTags', async () => {
    const tgDb = makeUniqueName('tg-db');
    await client.send(new CreateDatabaseCommand({ DatabaseName: tgDb }));
    try {
      const arn = `arn:aws:timestream:${region}:000000000000:database/${tgDb}`;
      await client.send(new TagResourceCommand({ ResourceARN: arn, Tags: [{ Key: 'k1', Value: 'v1' }, { Key: 'k2', Value: 'v2' }] }));
      const resp1 = await client.send(new ListTagsForResourceCommand({ ResourceARN: arn }));
      if (!resp1.Tags || resp1.Tags.find(t => t.Key === 'k1')?.Value !== 'v1') throw new Error('k1 tag not found');
      await client.send(new UntagResourceCommand({ ResourceARN: arn, TagKeys: ['k2'] }));
      const resp2 = await client.send(new ListTagsForResourceCommand({ ResourceARN: arn }));
      if (resp2.Tags?.find(t => t.Key === 'k2')) throw new Error('k2 should be removed');
    } finally {
      await safeCleanup(() => client.send(new DeleteDatabaseCommand({ DatabaseName: tgDb })));
    }
  });

  await r('TagResource_Database_Cleanup', async () => {
    await client.send(new DeleteDatabaseCommand({ DatabaseName: tagDb }));
  });

  const blDb = makeUniqueName('bl-db');
  const blTbl = makeUniqueName('bl-tbl');
  let blTaskId = '';
  let blSetup = false;

  await r('BatchLoad_Setup', async () => {
    await client.send(new CreateDatabaseCommand({ DatabaseName: blDb }));
    await client.send(new CreateTableCommand({ DatabaseName: blDb, TableName: blTbl }));
    blSetup = true;
  });

  await r('CreateBatchLoadTask', async () => {
    try {
      const resp = await client.send(new CreateBatchLoadTaskCommand({
        TargetDatabaseName: blDb,
        TargetTableName: blTbl,
        DataSourceConfiguration: {
          DataFormat: 'CSV',
          DataSourceS3Configuration: { BucketName: 'test-bucket' },
        },
        ReportConfiguration: {
          ReportS3Configuration: { BucketName: 'report-bucket' },
        },
        DataModelConfiguration: {
          DataModel: {
            DimensionMappings: [{ SourceColumn: 'col1', DestinationColumn: 'dim1' }],
            MeasureNameColumn: 'measure',
            TimeColumn: 'ts',
            TimeUnit: 'MILLISECONDS',
          },
        },
      }));
      if (!resp.TaskId) throw new Error('TaskId is null');
      blTaskId = resp.TaskId;
    } finally {
      if (!blTaskId && blSetup) {
        await safeCleanup(() => client.send(new DeleteTableCommand({ DatabaseName: blDb, TableName: blTbl })));
        await safeCleanup(() => client.send(new DeleteDatabaseCommand({ DatabaseName: blDb })));
      }
    }
  });

  await r('DescribeBatchLoadTask', async () => {
    const resp = await client.send(new DescribeBatchLoadTaskCommand({ TaskId: blTaskId }));
    if (!resp.BatchLoadTaskDescription) throw new Error('BatchLoadTaskDescription is null');
    if (!resp.BatchLoadTaskDescription.CreationTime) throw new Error('CreationTime is null');
  });

  await r('ListBatchLoadTasks', async () => {
    const resp = await client.send(new ListBatchLoadTasksCommand({}));
    if (!resp.BatchLoadTasks || resp.BatchLoadTasks.length === 0) throw new Error('expected at least 1 task, got 0');
  });

  await r('BatchLoad_Cleanup', async () => {
    if (blSetup) {
      await safeCleanup(() => client.send(new DeleteTableCommand({ DatabaseName: blDb, TableName: blTbl })));
      await safeCleanup(() => client.send(new DeleteDatabaseCommand({ DatabaseName: blDb })));
      blSetup = false;
    }
  });

  await r('DescribeBatchLoadTask_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new DescribeBatchLoadTaskCommand({ TaskId: 'nonexistent-task-id' }));
    }, 'ResourceNotFoundException');
  });

  return results;
}
