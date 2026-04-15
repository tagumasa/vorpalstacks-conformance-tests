import {
  ListDatabasesCommand,
  GetDatabaseCommand,
  ListTableMetadataCommand,
  GetTableMetadataCommand,
  ListEngineVersionsCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import { assertThrows } from '../../helpers.js';
import type { AthenaTestContext } from './context.js';

export async function runMetadataTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'ListDatabases', async () => {
    const resp = await client.send(new ListDatabasesCommand({ CatalogName: 'AwsDataCatalog' }));
    if (!resp.DatabaseList) throw new Error('DatabaseList to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetDatabase', async () => {
    const resp = await client.send(new GetDatabaseCommand({
      CatalogName: 'AwsDataCatalog',
      DatabaseName: 'default',
    }));
    if (!resp.Database) throw new Error('Database to be defined');
  }));

  results.push(await runner.runTest(svc, 'ListTableMetadata', async () => {
    const resp = await client.send(new ListTableMetadataCommand({
      CatalogName: 'AwsDataCatalog',
      DatabaseName: 'default',
    }));
    if (!resp) throw new Error('response to be defined');
  }));

  return results;
}

export async function runMetadataFinallyTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'GetTableMetadata_NonExistent', async () => {
    await assertThrows(async () => {
      await client.send(new GetTableMetadataCommand({
        CatalogName: 'AwsDataCatalog', DatabaseName: 'default', TableName: 'nonexistent_table_xyz',
      }));
    }, 'MetadataException');
  }));

  results.push(await runner.runTest(svc, 'ListEngineVersions', async () => {
    const resp = await client.send(new ListEngineVersionsCommand({}));
    if (!resp.EngineVersions || resp.EngineVersions.length === 0) {
      throw new Error('expected at least 1 engine version');
    }
  }));

  return results;
}
