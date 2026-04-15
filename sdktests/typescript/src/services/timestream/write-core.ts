import {
  TimestreamWriteClient,
  CreateDatabaseCommand,
  ListDatabasesCommand,
  DescribeDatabaseCommand,
  CreateTableCommand,
  ListTablesCommand,
  DescribeTableCommand,
  WriteRecordsCommand,
  UpdateTableCommand,
  DeleteTableCommand,
  UpdateDatabaseCommand,
  DeleteDatabaseCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-timestream-write';
import type { TestRunner, TestResult } from '../../runner.js';
import { makeUniqueName, assertThrows, safeCleanup } from '../../helpers.js';

export interface WriteCoreState {
  region: string;
  db: string;
  tbl: string;
  dbCreated: boolean;
  tblCreated: boolean;
}

export async function runWriteCoreTests(
  runner: TestRunner,
  client: TimestreamWriteClient,
  state: WriteCoreState,
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const s = 'timestream';
  const r = async (name: string, fn: () => Promise<void>) =>
    results.push(await runner.runTest(s, name, fn));

  try {
    await r('CreateDatabase', async () => {
      await client.send(new CreateDatabaseCommand({ DatabaseName: state.db }));
      state.dbCreated = true;
    });

    await r('ListDatabases', async () => {
      await client.send(new ListDatabasesCommand({ MaxResults: 10 }));
    });

    await r('DescribeDatabase', async () => {
      const resp = await client.send(new DescribeDatabaseCommand({ DatabaseName: state.db }));
      if (!resp.Database) throw new Error('Database is null');
      if (resp.Database.DatabaseName !== state.db) throw new Error('DatabaseName mismatch');
    });

    await r('CreateTable', async () => {
      await client.send(new CreateTableCommand({ DatabaseName: state.db, TableName: state.tbl }));
      state.tblCreated = true;
    });

    await r('ListTables', async () => {
      const resp = await client.send(new ListTablesCommand({ DatabaseName: state.db }));
      if (!resp.Tables) throw new Error('Tables is null');
      if (resp.Tables.length === 0) throw new Error('Tables is empty');
    });

    await r('DescribeTable', async () => {
      const resp = await client.send(new DescribeTableCommand({ DatabaseName: state.db, TableName: state.tbl }));
      if (!resp.Table) throw new Error('Table is null');
      if (resp.Table.TableName !== state.tbl) throw new Error('TableName mismatch');
      if (resp.Table.DatabaseName !== state.db) throw new Error('DatabaseName mismatch');
    });

    const t6 = Date.now() + 5;
    await r('WriteRecords', async () => {
      await client.send(new WriteRecordsCommand({
        DatabaseName: state.db, TableName: state.tbl,
        CommonAttributes: { Dimensions: [{ Name: 'env', Value: 'production' }] },
        Records: [
          { MeasureName: 'request_count', MeasureValue: '100', MeasureValueType: 'DOUBLE', Time: String(t6), TimeUnit: 'MILLISECONDS' },
          { MeasureName: 'error_count', MeasureValue: '5', MeasureValueType: 'DOUBLE', Time: String(t6 + 1000), TimeUnit: 'MILLISECONDS' },
          { MeasureName: 'latency_ms', MeasureValue: '42.3', MeasureValueType: 'DOUBLE', Time: String(t6 + 2000), TimeUnit: 'MILLISECONDS' },
        ],
      }));
    });

    await r('UpdateTable', async () => {
      await client.send(new UpdateTableCommand({
        DatabaseName: state.db, TableName: state.tbl,
        RetentionProperties: { MemoryStoreRetentionPeriodInHours: 24, MagneticStoreRetentionPeriodInDays: 7 },
      }));
    });

    await r('ListTagsForResource', async () => {
      const arn = `arn:aws:timestream:${state.region}:000000000000:database/${state.db}`;
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: arn }));
      const tags = resp.Tags ?? [];
      if (tags.length !== 0) throw new Error(`expected 0 tags on database created without tags, got ${tags.length}`);
    });

    await r('ListDatabases_Pagination', async () => {
      const resp = await client.send(new ListDatabasesCommand({ MaxResults: 1 }));
      if (!resp.Databases) throw new Error('Databases is null');
      if (resp.Databases.length > 1) throw new Error('Expected at most 1 database');
      if (resp.NextToken) {
        const resp2 = await client.send(new ListDatabasesCommand({ MaxResults: 1, NextToken: resp.NextToken }));
        if (!resp2.Databases) throw new Error('Second page Databases is null');
      }
    });

    const rtDb = makeUniqueName('ts-rt-db');
    const rtTbl = makeUniqueName('ts-rt-tbl');
    await r('WriteRecords', async () => {
      try {
        await client.send(new CreateDatabaseCommand({ DatabaseName: rtDb }));
        await client.send(new CreateTableCommand({
          DatabaseName: rtDb, TableName: rtTbl,
          RetentionProperties: { MemoryStoreRetentionPeriodInHours: 1, MagneticStoreRetentionPeriodInDays: 7 },
        }));
        const writeTime = Date.now();
        await client.send(new WriteRecordsCommand({
          DatabaseName: rtDb, TableName: rtTbl,
          Records: [{ Dimensions: [{ Name: 'device', Value: 'sensor-1' }], MeasureName: 'temperature', MeasureValue: '98.6', MeasureValueType: 'DOUBLE', Time: String(writeTime), TimeUnit: 'MILLISECONDS' }],
        }));
        const resp = await client.send(new DescribeTableCommand({ DatabaseName: rtDb, TableName: rtTbl }));
        if (!resp.Table) throw new Error('Table is null after write');
        if (resp.Table.TableName !== rtTbl) throw new Error('TableName mismatch after write');
      } finally {
        await safeCleanup(() => client.send(new DeleteTableCommand({ DatabaseName: rtDb, TableName: rtTbl })));
        await safeCleanup(() => client.send(new DeleteDatabaseCommand({ DatabaseName: rtDb })));
      }
    });

    await r('DescribeDatabase_NonExistent', async () => {
      await assertThrows(async () => {
        await client.send(new DescribeDatabaseCommand({ DatabaseName: 'nonexistent-database-xyz-12345' }));
      }, 'ResourceNotFoundException');
    });

    await r('DescribeTable_NonExistent', async () => {
      await assertThrows(async () => {
        await client.send(new DescribeTableCommand({ DatabaseName: state.db, TableName: 'nonexistent-table-xyz-12345' }));
      }, 'ResourceNotFoundException');
    });

    const dupDb = makeUniqueName('ts-dup-db');
    await r('CreateDatabase_Duplicate', async () => {
      try {
        await client.send(new CreateDatabaseCommand({ DatabaseName: dupDb }));
        try {
          await client.send(new CreateDatabaseCommand({ DatabaseName: dupDb }));
          throw new Error('expected an error');
        } catch (err: any) {
          if (err.name !== 'ConflictException' && err.name !== 'ResourceAlreadyExistsException') {
            throw new Error(`Expected ConflictException or ResourceAlreadyExistsException, got ${err.name}`);
          }
        }
      } finally {
        await safeCleanup(() => client.send(new DeleteDatabaseCommand({ DatabaseName: dupDb })));
      }
    });

    await r('DeleteTable', async () => {
      if (state.tblCreated) {
        await client.send(new DeleteTableCommand({ DatabaseName: state.db, TableName: state.tbl }));
        state.tblCreated = false;
      }
    });

    await r('UpdateDatabase', async () => {
      await client.send(new UpdateDatabaseCommand({
        DatabaseName: state.db,
        KmsKeyId: 'arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012',
      }));
    });

    await r('DeleteDatabase', async () => {
      if (state.dbCreated) {
        await client.send(new DeleteDatabaseCommand({ DatabaseName: state.db }));
        state.dbCreated = false;
      }
    });
  } finally {
    if (state.tblCreated) await safeCleanup(() => client.send(new DeleteTableCommand({ DatabaseName: state.db, TableName: state.tbl })));
    if (state.dbCreated) await safeCleanup(() => client.send(new DeleteDatabaseCommand({ DatabaseName: state.db })));
  }

  return results;
}
