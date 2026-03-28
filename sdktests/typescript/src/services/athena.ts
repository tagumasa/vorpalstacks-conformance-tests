import {
  AthenaClient,
  CreateWorkGroupCommand,
  GetWorkGroupCommand,
  ListWorkGroupsCommand,
  CreateDataCatalogCommand,
  GetDataCatalogCommand,
  ListDataCatalogsCommand,
  ListDatabasesCommand,
  GetDatabaseCommand,
  ListTableMetadataCommand,
  CreateNamedQueryCommand,
  GetNamedQueryCommand,
  ListNamedQueriesCommand,
  UpdateNamedQueryCommand,
  DeleteNamedQueryCommand,
  StartQueryExecutionCommand,
  GetQueryExecutionCommand,
  ListQueryExecutionsCommand,
  StopQueryExecutionCommand,
  UpdateWorkGroupCommand,
  DeleteWorkGroupCommand,
  DeleteDataCatalogCommand,
  AthenaServiceException,
} from '@aws-sdk/client-athena';
import { ResourceNotFoundException, InvalidRequestException } from '@aws-sdk/client-athena';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runAthenaTests(
  runner: TestRunner,
  athenaClient: AthenaClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const workGroupName = makeUniqueName('testwg');
  const catalogName = makeUniqueName('testcatalog');
  const namedQueryName = makeUniqueName('testquery');
  const updatedQueryName = makeUniqueName('updatedquery');
  const oldNameReusable = makeUniqueName('oldnamereuse');

  let namedQueryId = '';
  let reusableQueryId = '';
  let queryExecutionId = '';

  // ListWorkGroups
  results.push(
    await runner.runTest('athena', 'ListWorkGroups', async () => {
      const resp = await athenaClient.send(
        new ListWorkGroupsCommand({ MaxResults: 10 })
      );
      if (!resp.WorkGroups) throw new Error('WorkGroups is null');
    })
  );

  // CreateWorkGroup
  results.push(
    await runner.runTest('athena', 'CreateWorkGroup', async () => {
      await athenaClient.send(
        new CreateWorkGroupCommand({
          Name: workGroupName,
          Configuration: {
            ResultConfiguration: {
              OutputLocation: 's3://test-bucket/athena/',
            },
          },
        })
      );
    })
  );

  // GetWorkGroup
  results.push(
    await runner.runTest('athena', 'GetWorkGroup', async () => {
      const resp = await athenaClient.send(
        new GetWorkGroupCommand({ WorkGroup: workGroupName })
      );
      if (!resp.WorkGroup) throw new Error('WorkGroup is null');
    })
  );

  // ListDataCatalogs
  results.push(
    await runner.runTest('athena', 'ListDataCatalogs', async () => {
      const resp = await athenaClient.send(
        new ListDataCatalogsCommand({ MaxResults: 10 })
      );
      if (!resp.DataCatalogsSummary) throw new Error('DataCatalogsSummary is null');
    })
  );

  // CreateDataCatalog
  results.push(
    await runner.runTest('athena', 'CreateDataCatalog', async () => {
      await athenaClient.send(
        new CreateDataCatalogCommand({
          Name: catalogName,
          Type: 'GLUE',
          Description: 'Test catalog',
        })
      );
    })
  );

  // GetDataCatalog
  results.push(
    await runner.runTest('athena', 'GetDataCatalog', async () => {
      const resp = await athenaClient.send(
        new GetDataCatalogCommand({ Name: catalogName })
      );
      if (!resp.DataCatalog) throw new Error('DataCatalog is null');
    })
  );

  // ListDatabases
  results.push(
    await runner.runTest('athena', 'ListDatabases', async () => {
      const resp = await athenaClient.send(
        new ListDatabasesCommand({ CatalogName: 'AwsDataCatalog' })
      );
      if (!resp.DatabaseList) throw new Error('DatabaseList is null');
    })
  );

  // GetDatabase
  results.push(
    await runner.runTest('athena', 'GetDatabase', async () => {
      const resp = await athenaClient.send(
        new GetDatabaseCommand({
          CatalogName: 'AwsDataCatalog',
          DatabaseName: 'default',
        })
      );
      if (!resp.Database) throw new Error('Database is null');
    })
  );

  // ListTableMetadata
  results.push(
    await runner.runTest('athena', 'ListTableMetadata', async () => {
      const resp = await athenaClient.send(
        new ListTableMetadataCommand({
          CatalogName: 'AwsDataCatalog',
          DatabaseName: 'default',
        })
      );
    })
  );

  // CreateNamedQuery
  results.push(
    await runner.runTest('athena', 'CreateNamedQuery', async () => {
      const resp = await athenaClient.send(
        new CreateNamedQueryCommand({
          Name: namedQueryName,
          Database: 'default',
          QueryString: 'SELECT 1',
          Description: 'Test query',
        })
      );
      if (!resp.NamedQueryId) throw new Error('NamedQueryId is null');
      namedQueryId = resp.NamedQueryId;
    })
  );

  // GetNamedQuery
  results.push(
    await runner.runTest('athena', 'GetNamedQuery', async () => {
      const resp = await athenaClient.send(
        new GetNamedQueryCommand({ NamedQueryId: namedQueryId })
      );
      if (!resp.NamedQuery) throw new Error('NamedQuery is null');
    })
  );

  // ListNamedQueries
  results.push(
    await runner.runTest('athena', 'ListNamedQueries', async () => {
      const resp = await athenaClient.send(
        new ListNamedQueriesCommand({ MaxResults: 10 })
      );
      if (!resp.NamedQueryIds) throw new Error('NamedQueryIds is null');
    })
  );

  // UpdateNamedQuery
  results.push(
    await runner.runTest('athena', 'UpdateNamedQuery', async () => {
      await athenaClient.send(
        new UpdateNamedQueryCommand({
          NamedQueryId: namedQueryId,
          Name: updatedQueryName,
          Description: 'Updated test query',
          QueryString: 'SELECT 2',
        })
      );
    })
  );

  // GetNamedQuery_AfterUpdate
  results.push(
    await runner.runTest('athena', 'GetNamedQuery_AfterUpdate', async () => {
      const resp = await athenaClient.send(
        new GetNamedQueryCommand({ NamedQueryId: namedQueryId })
      );
      if (!resp.NamedQuery) throw new Error('NamedQuery is null');
      if (resp.NamedQuery.Name !== updatedQueryName) {
        throw new Error(`Expected name ${updatedQueryName}, got ${resp.NamedQuery.Name}`);
      }
      if (resp.NamedQuery.QueryString !== 'SELECT 2') {
        throw new Error(`Expected query 'SELECT 2', got ${resp.NamedQuery.QueryString}`);
      }
    })
  );

  // UpdateNamedQuery_OldNameReusable
  results.push(
    await runner.runTest('athena', 'UpdateNamedQuery_OldNameReusable', async () => {
      const createResp = await athenaClient.send(
        new CreateNamedQueryCommand({
          Name: oldNameReusable,
          Database: 'default',
          QueryString: 'SELECT 3',
        })
      );
      if (!createResp.NamedQueryId) throw new Error('NamedQueryId is null');
      reusableQueryId = createResp.NamedQueryId;

      const renamedName = makeUniqueName('renamedquery');
      await athenaClient.send(
        new UpdateNamedQueryCommand({
          NamedQueryId: reusableQueryId,
          Name: renamedName,
          Description: 'Renamed',
          QueryString: 'SELECT 4',
        })
      );

      const newResp = await athenaClient.send(
        new CreateNamedQueryCommand({
          Name: oldNameReusable,
          Database: 'default',
          QueryString: 'SELECT 5',
        })
      );
      if (!newResp.NamedQueryId) throw new Error('Should be able to reuse old name');
    })
  );

  // UpdateNamedQuery_NewNameNotReusable
  results.push(
    await runner.runTest('athena', 'UpdateNamedQuery_NewNameNotReusable', async () => {
      try {
        await athenaClient.send(
          new CreateNamedQueryCommand({
            Name: updatedQueryName,
            Database: 'default',
            QueryString: 'SELECT duplicate',
          })
        );
        throw new Error('Should have failed with error');
      } catch (err) {
        if (!(err instanceof InvalidRequestException) && !(err instanceof AthenaServiceException) && !(err instanceof Error && (err.name === 'InvalidRequestException' || err.name === 'AthenaServiceException'))) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected error, got ${name}`);
        }
      }
    })
  );

  // DeleteNamedQuery
  results.push(
    await runner.runTest('athena', 'DeleteNamedQuery', async () => {
      await athenaClient.send(
        new DeleteNamedQueryCommand({ NamedQueryId: namedQueryId })
      );
    })
  );

  // StartQueryExecution
  results.push(
    await runner.runTest('athena', 'StartQueryExecution', async () => {
      const resp = await athenaClient.send(
        new StartQueryExecutionCommand({
          QueryString: 'SELECT 1',
          QueryExecutionContext: {
            Database: 'default',
          },
          ResultConfiguration: {
            OutputLocation: 's3://test-bucket/athena/',
          },
        })
      );
      if (!resp.QueryExecutionId) throw new Error('QueryExecutionId is null');
      queryExecutionId = resp.QueryExecutionId;
    })
  );

  // GetQueryExecution
  results.push(
    await runner.runTest('athena', 'GetQueryExecution', async () => {
      const resp = await athenaClient.send(
        new GetQueryExecutionCommand({ QueryExecutionId: queryExecutionId })
      );
      if (!resp.QueryExecution) throw new Error('QueryExecution is null');
    })
  );

  // ListQueryExecutions
  results.push(
    await runner.runTest('athena', 'ListQueryExecutions', async () => {
      const resp = await athenaClient.send(
        new ListQueryExecutionsCommand({ MaxResults: 10 })
      );
      if (!resp.QueryExecutionIds) throw new Error('QueryExecutionIds is null');
    })
  );

  // StopQueryExecution
  results.push(
    await runner.runTest('athena', 'StopQueryExecution', async () => {
      const getResp = await athenaClient.send(
        new GetQueryExecutionCommand({ QueryExecutionId: queryExecutionId })
      );
      const state = getResp.QueryExecution?.Status?.State;
      if (state === 'QUEUED' || state === 'RUNNING') {
        await athenaClient.send(
          new StopQueryExecutionCommand({ QueryExecutionId: queryExecutionId })
        );
      }
    })
  );

  // UpdateWorkGroup
  results.push(
    await runner.runTest('athena', 'UpdateWorkGroup', async () => {
      await athenaClient.send(
        new UpdateWorkGroupCommand({
          WorkGroup: workGroupName,
          Description: 'Updated work group',
        })
      );
    })
  );

  // DeleteWorkGroup
  results.push(
    await runner.runTest('athena', 'DeleteWorkGroup', async () => {
      await athenaClient.send(
        new DeleteWorkGroupCommand({
          WorkGroup: workGroupName,
          RecursiveDeleteOption: true,
        })
      );
    })
  );

  // DeleteDataCatalog
  results.push(
    await runner.runTest('athena', 'DeleteDataCatalog', async () => {
      await athenaClient.send(
        new DeleteDataCatalogCommand({ Name: catalogName })
      );
    })
  );

  // Error cases
  results.push(
    await runner.runTest('athena', 'GetWorkGroup_NonExistent', async () => {
      try {
        await athenaClient.send(
          new GetWorkGroupCommand({ WorkGroup: 'nonexistent_wg_xyz' })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('athena', 'DeleteWorkGroup_NonExistent', async () => {
      try {
        await athenaClient.send(
          new DeleteWorkGroupCommand({ WorkGroup: 'nonexistent_wg_xyz' })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('athena', 'GetNamedQuery_NonExistent', async () => {
      try {
        await athenaClient.send(
          new GetNamedQueryCommand({ NamedQueryId: '00000000-0000-0000-0000-000000000000' })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('athena', 'GetDataCatalog_NonExistent', async () => {
      try {
        await athenaClient.send(
          new GetDataCatalogCommand({ Name: 'nonexistent_catalog_xyz' })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  results.push(
    await runner.runTest('athena', 'CreateWorkGroup_Duplicate', async () => {
      const dupWGName = makeUniqueName('dupwg');
      try {
        await athenaClient.send(
          new CreateWorkGroupCommand({ Name: dupWGName })
        );
      } catch {
        // first create failed, skip cleanup
      }

      try {
        await athenaClient.send(
          new CreateWorkGroupCommand({ Name: dupWGName })
        );
        throw new Error('Expected error but got none');
      } catch (err) {
        if (!(err instanceof InvalidRequestException) && !(err instanceof AthenaServiceException) && !(err instanceof Error && (err.name === 'InvalidRequestException' || err.name === 'AthenaServiceException'))) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected error, got ${name}`);
        }
      } finally {
        try {
          await athenaClient.send(
            new DeleteWorkGroupCommand({ WorkGroup: dupWGName, RecursiveDeleteOption: true })
          );
        } catch { /* ignore */ }
      }
    })
  );

  return results;
}