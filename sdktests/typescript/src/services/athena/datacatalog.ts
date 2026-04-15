import {
  CreateDataCatalogCommand,
  GetDataCatalogCommand,
  UpdateDataCatalogCommand,
  DeleteDataCatalogCommand,
  ListDataCatalogsCommand,
  TagResourceCommand,
  ListTagsForResourceCommand,
} from '@aws-sdk/client-athena';
import type { TestResult } from '../../runner.js';
import { assertThrows, makeUniqueName, safeCleanup } from '../../helpers.js';
import type { AthenaTestContext } from './context.js';

export async function runDataCatalogTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  results.push(await runner.runTest(svc, 'ListDataCatalogs', async () => {
    const resp = await client.send(new ListDataCatalogsCommand({ MaxResults: 10 }));
    if (!resp.DataCatalogsSummary) throw new Error('DataCatalogsSummary to be defined');
  }));

  results.push(await runner.runTest(svc, 'CreateDataCatalog', async () => {
    await client.send(new CreateDataCatalogCommand({
      Name: ctx.catalogName,
      Type: 'GLUE',
      Description: 'Test catalog',
      Tags: [{ Key: 'source', Value: 'test' }],
    }));
  }));

  results.push(await runner.runTest(svc, 'GetDataCatalog', async () => {
    const resp = await client.send(new GetDataCatalogCommand({ Name: ctx.catalogName }));
    if (!resp.DataCatalog) throw new Error('DataCatalog to be defined');
    if (resp.DataCatalog.Name !== ctx.catalogName) throw new Error('name mismatch');
  }));

  results.push(await runner.runTest(svc, 'UpdateDataCatalog', async () => {
    await client.send(new UpdateDataCatalogCommand({
      Name: ctx.catalogName,
      Type: 'GLUE',
      Description: 'Updated catalog',
    }));
  }));

  results.push(await runner.runTest(svc, 'TagResource_DataCatalog', async () => {
    const arn = `arn:aws:athena:${ctx.svcCtx.region}:000000000000:datacatalog/${ctx.catalogName}`;
    await client.send(new TagResourceCommand({
      ResourceARN: arn,
      Tags: [{ Key: 'extra', Value: 'dc-tag' }],
    }));
    const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: arn }));
    const keys = resp.Tags?.map((t) => t.Key) ?? [];
    if (!keys.includes('extra')) throw new Error('tag not applied');
  }));

  results.push(await runner.runTest(svc, 'DeleteDataCatalog', async () => {
    const resp = await client.send(new DeleteDataCatalogCommand({ Name: ctx.catalogName }));
    if (!resp) throw new Error('response to be defined');
  }));

  results.push(await runner.runTest(svc, 'GetDataCatalog_NonExistent', async () => {
    await assertThrows(() => client.send(new GetDataCatalogCommand({ Name: 'nonexistent_catalog_xyz' })), 'ResourceNotFoundException');
  }));

  return results;
}

export async function runDataCatalogFinallyTests(ctx: AthenaTestContext): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const { client, runner } = ctx;
  const svc = 'athena';

  const tagCatName = makeUniqueName('athena-tagcat');
  const tagCatArn = `arn:aws:athena:${ctx.svcCtx.region}:000000000000:datacatalog/${tagCatName}`;

  results.push(await runner.runTest(svc, 'DeleteDataCatalog_TagCleanup', async () => {
    try {
      await client.send(new CreateDataCatalogCommand({ Name: tagCatName, Type: 'GLUE', Description: 'Tag test' }));
      await client.send(new TagResourceCommand({
        ResourceARN: tagCatArn, Tags: [{ Key: 'purpose', Value: 'testing' }],
      }));
      const resp = await client.send(new ListTagsForResourceCommand({ ResourceARN: tagCatArn }));
      if (!resp.Tags || resp.Tags.length < 1) throw new Error('expected at least 1 tag on datacatalog');
    } finally {
      await safeCleanup(() => client.send(new DeleteDataCatalogCommand({ Name: tagCatName })));
    }
  }));

  const udcCatName = makeUniqueName('athena-udccat');

  results.push(await runner.runTest(svc, 'UpdateDataCatalog_Setup', async () => {
    await client.send(new CreateDataCatalogCommand({ Name: udcCatName, Type: 'GLUE', Description: 'Before update' }));
  }));

  results.push(await runner.runTest(svc, 'UpdateDataCatalog_Verify', async () => {
    await client.send(new UpdateDataCatalogCommand({ Name: udcCatName, Type: 'GLUE', Description: 'After update' }));
    const resp = await client.send(new GetDataCatalogCommand({ Name: udcCatName }));
    if (resp.DataCatalog?.Description !== 'After update') {
      throw new Error(`expected 'After update', got ${resp.DataCatalog?.Description}`);
    }
  }));

  results.push(await runner.runTest(svc, 'DeleteDataCatalog_UDCCleanup', async () => {
    await client.send(new DeleteDataCatalogCommand({ Name: udcCatName }));
  }));

  return results;
}
