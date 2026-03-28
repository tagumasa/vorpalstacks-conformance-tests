import {
  DynamoDBClient,
  CreateTableCommand,
  DescribeTableCommand,
  ListTablesCommand,
  DeleteTableCommand,
  PutItemCommand,
  GetItemCommand,
  UpdateItemCommand,
  DeleteItemCommand,
  QueryCommand,
  ScanCommand,
  BatchWriteItemCommand,
  BatchGetItemCommand,
  TagResourceCommand,
  ListTagsOfResourceCommand,
  UntagResourceCommand,
  UpdateTimeToLiveCommand,
  DescribeTimeToLiveCommand,
  CreateBackupCommand,
  ListBackupsCommand,
  DescribeContinuousBackupsCommand,
  UpdateContinuousBackupsCommand,
  ExecuteStatementCommand,
  TransactWriteItemsCommand,
  TransactGetItemsCommand,
  BatchExecuteStatementCommand,
  ExecuteTransactionCommand,
  UpdateTableCommand,
} from '@aws-sdk/client-dynamodb';
import { ResourceNotFoundException, ConditionalCheckFailedException, ResourceInUseException } from '@aws-sdk/client-dynamodb';
import { TestRunner } from '../runner.js';
import { TestResult } from '../runner.js';

function makeUniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 999999)}`;
}

export async function runDynamoDBTests(
  runner: TestRunner,
  dynamodbClient: DynamoDBClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  const tableName = makeUniqueName('TSTable');
  const tableARN = `arn:aws:dynamodb:${region}:000000000000:table/${tableName}`;

  // CreateTable
  results.push(
    await runner.runTest('dynamodb', 'CreateTable', async () => {
      await dynamodbClient.send(new CreateTableCommand({
        TableName: tableName,
        AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
        KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
        BillingMode: 'PAY_PER_REQUEST',
      }));
    })
  );

  // DescribeTable
  results.push(
    await runner.runTest('dynamodb', 'DescribeTable', async () => {
      const resp = await dynamodbClient.send(new DescribeTableCommand({ TableName: tableName }));
      if (!resp.Table) throw new Error('Table is null');
    })
  );

  // ListTables
  results.push(
    await runner.runTest('dynamodb', 'ListTables', async () => {
      const resp = await dynamodbClient.send(new ListTablesCommand({}));
      if (!resp.TableNames) throw new Error('TableNames is null');
    })
  );

  // PutItem
  results.push(
    await runner.runTest('dynamodb', 'PutItem', async () => {
      await dynamodbClient.send(new PutItemCommand({
        TableName: tableName,
        Item: { id: { S: 'test1' }, name: { S: 'Test Item' }, count: { N: '42' } },
      }));
    })
  );

  // GetItem
  results.push(
    await runner.runTest('dynamodb', 'GetItem', async () => {
      const resp = await dynamodbClient.send(new GetItemCommand({
        TableName: tableName,
        Key: { id: { S: 'test1' } },
      }));
      if (!resp.Item) throw new Error('Item is null');
      if (resp.Item.name?.S !== 'Test Item') throw new Error('item name mismatch');
    })
  );

  // UpdateItem
  results.push(
    await runner.runTest('dynamodb', 'UpdateItem', async () => {
      const resp = await dynamodbClient.send(new UpdateItemCommand({
        TableName: tableName,
        Key: { id: { S: 'test1' } },
        UpdateExpression: 'SET #n = :name',
        ExpressionAttributeNames: { '#n': 'name' },
        ExpressionAttributeValues: { ':name': { S: 'Updated' } },
        ReturnValues: 'ALL_NEW',
      }));
      if (!resp.Attributes) throw new Error('attributes not found');
    })
  );

  // Query
  results.push(
    await runner.runTest('dynamodb', 'Query', async () => {
      const resp = await dynamodbClient.send(new QueryCommand({
        TableName: tableName,
        KeyConditionExpression: 'id = :id',
        ExpressionAttributeValues: { ':id': { S: 'test1' } },
      }));
      if (!resp.Count || resp.Count === 0) throw new Error('No items found');
    })
  );

  // Scan
  results.push(
    await runner.runTest('dynamodb', 'Scan', async () => {
      const resp = await dynamodbClient.send(new ScanCommand({ TableName: tableName }));
      if (!resp.Count || resp.Count === 0) throw new Error('No items found');
    })
  );

  // BatchWriteItem
  results.push(
    await runner.runTest('dynamodb', 'BatchWriteItem', async () => {
      const resp = await dynamodbClient.send(new BatchWriteItemCommand({
        RequestItems: {
          [tableName]: [
            { PutRequest: { Item: { id: { S: 'batch1' }, data: { S: 'batch item 1' } } } },
            { PutRequest: { Item: { id: { S: 'batch2' }, data: { S: 'batch item 2' } } } },
          ],
        },
      }));
      if (resp.UnprocessedItems === undefined) throw new Error('UnprocessedItems is undefined');
    })
  );

  // BatchGetItem
  results.push(
    await runner.runTest('dynamodb', 'BatchGetItem', async () => {
      const resp = await dynamodbClient.send(new BatchGetItemCommand({
        RequestItems: {
          [tableName]: { Keys: [{ id: { S: 'batch1' } }, { id: { S: 'batch2' } }] },
        },
      }));
      if (!resp.Responses) throw new Error('Responses is undefined');
      const tableResponses = resp.Responses[tableName];
      if (!tableResponses || tableResponses.length === 0) throw new Error('No responses for table');
    })
  );

  // DeleteItem
  results.push(
    await runner.runTest('dynamodb', 'DeleteItem', async () => {
      await dynamodbClient.send(new DeleteItemCommand({
        TableName: tableName,
        Key: { id: { S: 'test1' } },
      }));
    })
  );

  // TagResource
  results.push(
    await runner.runTest('dynamodb', 'TagResource', async () => {
      await dynamodbClient.send(new TagResourceCommand({
        ResourceArn: tableARN,
        Tags: [{ Key: 'Environment', Value: 'Test' }],
      }));
    })
  );

  // ListTagsOfResource
  results.push(
    await runner.runTest('dynamodb', 'ListTagsOfResource', async () => {
      const resp = await dynamodbClient.send(new ListTagsOfResourceCommand({
        ResourceArn: tableARN,
      }));
      if (!resp.Tags || resp.Tags.length === 0) throw new Error('no tags found');
    })
  );

  // UntagResource
  results.push(
    await runner.runTest('dynamodb', 'UntagResource', async () => {
      await dynamodbClient.send(new UntagResourceCommand({
        ResourceArn: tableARN,
        TagKeys: ['Environment'],
      }));
    })
  );

  // UpdateTimeToLive
  results.push(
    await runner.runTest('dynamodb', 'UpdateTimeToLive', async () => {
      const resp = await dynamodbClient.send(new UpdateTimeToLiveCommand({
        TableName: tableName,
        TimeToLiveSpecification: {
          AttributeName: 'ttl',
          Enabled: true,
        },
      }));
      if (!resp.TimeToLiveSpecification) throw new Error('TimeToLiveSpecification is nil');
    })
  );

  // DescribeTimeToLive
  results.push(
    await runner.runTest('dynamodb', 'DescribeTimeToLive', async () => {
      const resp = await dynamodbClient.send(new DescribeTimeToLiveCommand({
        TableName: tableName,
      }));
      if (!resp.TimeToLiveDescription) throw new Error('TTL description not found');
    })
  );

  // CreateBackup
  results.push(
    await runner.runTest('dynamodb', 'CreateBackup', async () => {
      const backupName = makeUniqueName('TSBackup');
      const resp = await dynamodbClient.send(new CreateBackupCommand({
        TableName: tableName,
        BackupName: backupName,
      }));
      if (!resp.BackupDetails) throw new Error('BackupDetails is nil');
    })
  );

  // ListBackups
  results.push(
    await runner.runTest('dynamodb', 'ListBackups', async () => {
      const resp = await dynamodbClient.send(new ListBackupsCommand({}));
      if (!resp.BackupSummaries) throw new Error('backup summaries is nil');
    })
  );

  // DescribeContinuousBackups
  results.push(
    await runner.runTest('dynamodb', 'DescribeContinuousBackups', async () => {
      const resp = await dynamodbClient.send(new DescribeContinuousBackupsCommand({
        TableName: tableName,
      }));
      if (!resp.ContinuousBackupsDescription) throw new Error('continuous backups description not found');
    })
  );

  // UpdateContinuousBackups
  results.push(
    await runner.runTest('dynamodb', 'UpdateContinuousBackups', async () => {
      const resp = await dynamodbClient.send(new UpdateContinuousBackupsCommand({
        TableName: tableName,
        PointInTimeRecoverySpecification: {
          PointInTimeRecoveryEnabled: true,
        },
      }));
      if (!resp.ContinuousBackupsDescription) throw new Error('ContinuousBackupsDescription is nil');
    })
  );

  // ExecuteStatement (PartiQL INSERT)
  results.push(
    await runner.runTest('dynamodb', 'ExecuteStatement (PartiQL)', async () => {
      const resp = await dynamodbClient.send(new ExecuteStatementCommand({
        Statement: `INSERT INTO "${tableName}" VALUE {'id': 'partiql1', 'name': 'PartiQL Item'}`,
      }));
      if (!resp) throw new Error('ExecuteStatement response is nil');
    })
  );

  // ExecuteStatement (SELECT)
  results.push(
    await runner.runTest('dynamodb', 'ExecuteStatement (SELECT)', async () => {
      const resp = await dynamodbClient.send(new ExecuteStatementCommand({
        Statement: `SELECT * FROM "${tableName}" WHERE id = 'partiql1'`,
      }));
      if (!resp.Items || resp.Items.length === 0) throw new Error('no items found');
    })
  );

  // TransactWriteItems
  results.push(
    await runner.runTest('dynamodb', 'TransactWriteItems', async () => {
      const resp = await dynamodbClient.send(new TransactWriteItemsCommand({
        TransactItems: [
          {
            Put: {
              TableName: tableName,
              Item: { id: { S: 'transact1' }, name: { S: 'Transact Item' } },
            },
          },
        ],
      }));
      if (!resp) throw new Error('TransactWriteItems response is nil');
    })
  );

  // TransactGetItems
  results.push(
    await runner.runTest('dynamodb', 'TransactGetItems', async () => {
      const resp = await dynamodbClient.send(new TransactGetItemsCommand({
        TransactItems: [
          {
            Get: {
              TableName: tableName,
              Key: { id: { S: 'transact1' } },
            },
          },
        ],
      }));
      if (!resp.Responses || resp.Responses.length === 0) throw new Error('no responses');
    })
  );

  // BatchExecuteStatement
  results.push(
    await runner.runTest('dynamodb', 'BatchExecuteStatement', async () => {
      const resp = await dynamodbClient.send(new BatchExecuteStatementCommand({
        Statements: [
          {
            Statement: `UPDATE "${tableName}" SET #n = :name WHERE id = 'batch1'`,
            Parameters: [{ S: 'Updated via Batch' }],
          },
        ],
      }));
      if (!resp.Responses) throw new Error('BatchExecuteStatement Responses is nil');
    })
  );

  // ExecuteTransaction
  results.push(
    await runner.runTest('dynamodb', 'ExecuteTransaction', async () => {
      const resp = await dynamodbClient.send(new ExecuteTransactionCommand({
        TransactStatements: [
          {
            Statement: `SELECT * FROM "${tableName}" WHERE id = 'transact1'`,
          },
        ],
      }));
      if (!resp.Responses || resp.Responses.length === 0) throw new Error('no responses');
    })
  );

  // UpdateTable
  results.push(
    await runner.runTest('dynamodb', 'UpdateTable', async () => {
      const resp = await dynamodbClient.send(new UpdateTableCommand({
        TableName: tableName,
      }));
      if (!resp.TableDescription) throw new Error('TableDescription is nil');
    })
  );

  // DeleteTable
  results.push(
    await runner.runTest('dynamodb', 'DeleteTable', async () => {
      await dynamodbClient.send(new DeleteTableCommand({ TableName: tableName }));
    })
  );

  // === ERROR / EDGE CASE TESTS ===

  // GetItem_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'GetItem_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new GetItemCommand({
          TableName: 'NonExistentTable_xyz',
          Key: { id: { S: 'test1' } },
        }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // PutItem_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'PutItem_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new PutItemCommand({
          TableName: 'NonExistentTable_xyz',
          Item: { id: { S: 'k' } },
        }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // Query_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'Query_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new QueryCommand({
          TableName: 'NonExistentTable_xyz',
          KeyConditionExpression: 'id = :id',
          ExpressionAttributeValues: { ':id': { S: 'k' } },
        }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // Scan_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'Scan_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new ScanCommand({ TableName: 'NonExistentTable_xyz' }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // DescribeTable_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'DescribeTable_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new DescribeTableCommand({ TableName: 'NonExistentTable_xyz' }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // DeleteTable_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'DeleteTable_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new DeleteTableCommand({ TableName: 'NonExistentTable_xyz' }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // UpdateItem_ConditionalCheckFail
  results.push(
    await runner.runTest('dynamodb', 'UpdateItem_ConditionalCheckFail', async () => {
      const errTable = makeUniqueName('CondTable');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: errTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        await dynamodbClient.send(new PutItemCommand({
          TableName: errTable,
          Item: { id: { S: 'cond1' }, status: { S: 'active' } },
        }));
        try {
          await dynamodbClient.send(new UpdateItemCommand({
            TableName: errTable,
            Key: { id: { S: 'cond1' } },
            UpdateExpression: 'SET #s = :val',
            ConditionExpression: '#s = :expected',
            ExpressionAttributeNames: { '#s': 'status' },
            ExpressionAttributeValues: {
              ':val': { S: 'inactive' },
              ':expected': { S: 'deleted' },
            },
          }));
          throw new Error('expected ConditionalCheckFailedException');
        } catch (err) {
          if (!(err instanceof ConditionalCheckFailedException)) {
            const name = err instanceof Error ? err.constructor.name : String(err);
            throw new Error(`Expected ConditionalCheckFailedException, got ${name}`);
          }
        }
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: errTable })); } catch { /* ignore */ }
      }
    })
  );

  // GetItem_NonExistentKey
  results.push(
    await runner.runTest('dynamodb', 'GetItem_NonExistentKey', async () => {
      const errTable = makeUniqueName('GetItemErr');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: errTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        const resp = await dynamodbClient.send(new GetItemCommand({
          TableName: errTable,
          Key: { id: { S: 'nonexistent' } },
        }));
        if (resp.Item && Object.keys(resp.Item).length !== 0) {
          throw new Error('expected empty item for non-existent key');
        }
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: errTable })); } catch { /* ignore */ }
      }
    })
  );

  // DeleteItem_ConditionalCheckFail
  results.push(
    await runner.runTest('dynamodb', 'DeleteItem_ConditionalCheckFail', async () => {
      const errTable = makeUniqueName('DelCondTable');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: errTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        await dynamodbClient.send(new PutItemCommand({
          TableName: errTable,
          Item: { id: { S: 'del1' }, status: { S: 'active' } },
        }));
        try {
          await dynamodbClient.send(new DeleteItemCommand({
            TableName: errTable,
            Key: { id: { S: 'del1' } },
            ConditionExpression: 'attribute_not_exists(id)',
          }));
          throw new Error('expected ConditionalCheckFailedException');
        } catch (err) {
          if (!(err instanceof ConditionalCheckFailedException)) {
            const name = err instanceof Error ? err.constructor.name : String(err);
            throw new Error(`Expected ConditionalCheckFailedException, got ${name}`);
          }
        }
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: errTable })); } catch { /* ignore */ }
      }
    })
  );

  // BatchGetItem_NonExistentTable
  results.push(
    await runner.runTest('dynamodb', 'BatchGetItem_NonExistentTable', async () => {
      try {
        await dynamodbClient.send(new BatchGetItemCommand({
          RequestItems: {
            'NonExistentTable_xyz': { Keys: [{ id: { S: 'k' } }] },
          },
        }));
        throw new Error('expected error for non-existent table');
      } catch (err) {
        if (!(err instanceof ResourceNotFoundException)) {
          const name = err instanceof Error ? err.constructor.name : String(err);
          throw new Error(`Expected ResourceNotFoundException, got ${name}`);
        }
      }
    })
  );

  // CreateTable_DuplicateName
  results.push(
    await runner.runTest('dynamodb', 'CreateTable_DuplicateName', async () => {
      const dupTable = makeUniqueName('DupTable');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: dupTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        try {
          await dynamodbClient.send(new CreateTableCommand({
            TableName: dupTable,
            AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
            KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
            BillingMode: 'PAY_PER_REQUEST',
          }));
          throw new Error('expected error for duplicate table name');
        } catch (err) {
          if (!(err instanceof ResourceInUseException)) {
            const name = err instanceof Error ? err.constructor.name : String(err);
            throw new Error(`Expected ResourceInUseException, got ${name}`);
          }
        }
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: dupTable })); } catch { /* ignore */ }
      }
    })
  );

  // Query_ReturnConsumedCapacity
  results.push(
    await runner.runTest('dynamodb', 'Query_ReturnConsumedCapacity', async () => {
      const qTable = makeUniqueName('QCapTable');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: qTable,
          AttributeDefinitions: [{ AttributeName: 'pk', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'pk', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        await dynamodbClient.send(new PutItemCommand({
          TableName: qTable,
          Item: { pk: { S: 'key1' } },
        }));
        const resp = await dynamodbClient.send(new QueryCommand({
          TableName: qTable,
          KeyConditionExpression: 'pk = :pk',
          ExpressionAttributeValues: { ':pk': { S: 'key1' } },
          ReturnConsumedCapacity: 'TOTAL',
        }));
        if (!resp.ConsumedCapacity) throw new Error('expected ConsumedCapacity in response');
        if (resp.ConsumedCapacity.TableName !== qTable) throw new Error('ConsumedCapacity.TableName mismatch');
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: qTable })); } catch { /* ignore */ }
      }
    })
  );

  // PutItem_ReturnValues
  results.push(
    await runner.runTest('dynamodb', 'PutItem_ReturnValues', async () => {
      const rvTable = makeUniqueName('RVTable');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: rvTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        const resp1 = await dynamodbClient.send(new PutItemCommand({
          TableName: rvTable,
          Item: { id: { S: 'rv1' }, name: { S: 'Alice' }, count: { N: '10' } },
          ReturnValues: 'ALL_OLD',
        }));
        if (resp1.Attributes) throw new Error('first PutItem with ReturnValues=ALL_OLD should have nil Attributes');

        const resp2 = await dynamodbClient.send(new PutItemCommand({
          TableName: rvTable,
          Item: { id: { S: 'rv1' }, name: { S: 'Bob' }, count: { N: '20' } },
          ReturnValues: 'ALL_OLD',
        }));
        if (!resp2.Attributes) throw new Error('second PutItem with ReturnValues=ALL_OLD should return old attributes');
        if (resp2.Attributes.name?.S !== 'Alice') throw new Error(`old name should be 'Alice', got ${resp2.Attributes.name?.S}`);
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: rvTable })); } catch { /* ignore */ }
      }
    })
  );

  // UpdateItem_ReturnUpdatedAttributes
  results.push(
    await runner.runTest('dynamodb', 'UpdateItem_ReturnUpdatedAttributes', async () => {
      const uaTable = makeUniqueName('UATable');
      try {
        await dynamodbClient.send(new CreateTableCommand({
          TableName: uaTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        await dynamodbClient.send(new PutItemCommand({
          TableName: uaTable,
          Item: {
            id: { S: 'ua1' },
            val: { N: '0' },
            tags: { L: [{ S: 'a' }] },
          },
        }));
        const resp = await dynamodbClient.send(new UpdateItemCommand({
          TableName: uaTable,
          Key: { id: { S: 'ua1' } },
          UpdateExpression: 'ADD #v :inc, SET #t = list_append(#t, :newTag)',
          ExpressionAttributeNames: { '#v': 'val', '#t': 'tags' },
          ExpressionAttributeValues: {
            ':inc': { N: '5' },
            ':newTag': { L: [{ S: 'b' }] },
          },
          ReturnValues: 'ALL_NEW',
        }));
        if (!resp.Attributes) throw new Error('expected updated attributes');
        if (resp.Attributes.val?.N !== '5') throw new Error(`expected val=5, got ${resp.Attributes.val?.N}`);
        const tags = resp.Attributes.tags?.L;
        if (!tags || tags.length !== 2) throw new Error(`expected 2 tags, got ${tags?.length}`);
      } finally {
        try { await dynamodbClient.send(new DeleteTableCommand({ TableName: uaTable })); } catch { /* ignore */ }
      }
    })
  );

  return results;
}
