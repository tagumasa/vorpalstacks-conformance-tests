import {
  CreateTableCommand,
  DescribeTableCommand,
  ListTablesCommand,
  UpdateTableCommand,
  DeleteTableCommand,
  PutItemCommand,
  GetItemCommand,
  QueryCommand,
  ScanCommand,
  BatchGetItemCommand,
  TagResourceCommand,
  ListTagsOfResourceCommand,
  UntagResourceCommand,
  UpdateTimeToLiveCommand,
  DescribeTimeToLiveCommand,
  CreateBackupCommand,
  DeleteBackupCommand,
  ListBackupsCommand,
  DescribeContinuousBackupsCommand,
  UpdateContinuousBackupsCommand,
  CreateGlobalTableCommand,
  ListGlobalTablesCommand,
} from '@aws-sdk/client-dynamodb';
import { DynamoDBTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runTableTests(ctx: DynamoDBTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, tableName, tableARN } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'CreateTable', async () => {
    const resp = await client.send(new CreateTableCommand({
      TableName: tableName,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    if (!resp.TableDescription) throw new Error('TableDescription to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'DescribeTable', async () => {
    const resp = await client.send(new DescribeTableCommand({ TableName: tableName }));
    if (!resp.Table) throw new Error('table not found');
  }));

  results.push(await runner.runTest('dynamodb', 'ListTables', async () => {
    const resp = await client.send(new ListTablesCommand({}));
    if (!resp.TableNames) throw new Error('table names to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'TagResource', async () => {
    await client.send(new TagResourceCommand({
      ResourceArn: tableARN,
      Tags: [{ Key: 'Environment', Value: 'Test' }],
    }));
  }));

  results.push(await runner.runTest('dynamodb', 'ListTagsOfResource', async () => {
    const resp = await client.send(new ListTagsOfResourceCommand({ ResourceArn: tableARN }));
    if (!resp.Tags || resp.Tags.length === 0) throw new Error('no tags found');
  }));

  results.push(await runner.runTest('dynamodb', 'UntagResource', async () => {
    await client.send(new UntagResourceCommand({
      ResourceArn: tableARN,
      TagKeys: ['Environment'],
    }));
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateTimeToLive', async () => {
    const resp = await client.send(new UpdateTimeToLiveCommand({
      TableName: tableName,
      TimeToLiveSpecification: { AttributeName: 'ttl', Enabled: true },
    }));
    if (!resp.TimeToLiveSpecification) throw new Error('TimeToLiveSpecification to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'DescribeTimeToLive', async () => {
    const resp = await client.send(new DescribeTimeToLiveCommand({ TableName: tableName }));
    if (!resp.TimeToLiveDescription) throw new Error('TTL description not found');
  }));

  results.push(await runner.runTest('dynamodb', 'CreateBackup', async () => {
    const backupName = `TestBackup-${ts}`;
    const resp = await client.send(new CreateBackupCommand({
      TableName: tableName,
      BackupName: backupName,
    }));
    if (!resp.BackupDetails) throw new Error('BackupDetails to be defined');
    await safeCleanup(async () => {
      if (resp.BackupDetails?.BackupArn) {
        await client.send(new DeleteBackupCommand({ BackupArn: resp.BackupDetails.BackupArn }));
      }
    });
  }));

  results.push(await runner.runTest('dynamodb', 'ListBackups', async () => {
    const resp = await client.send(new ListBackupsCommand({}));
    if (!resp.BackupSummaries) throw new Error('backup summaries to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'DescribeContinuousBackups', async () => {
    const resp = await client.send(new DescribeContinuousBackupsCommand({ TableName: tableName }));
    if (!resp.ContinuousBackupsDescription) throw new Error('continuous backups description not found');
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateContinuousBackups', async () => {
    const resp = await client.send(new UpdateContinuousBackupsCommand({
      TableName: tableName,
      PointInTimeRecoverySpecification: { PointInTimeRecoveryEnabled: true },
    }));
    if (!resp.ContinuousBackupsDescription) throw new Error('ContinuousBackupsDescription to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateTable', async () => {
    const resp = await client.send(new UpdateTableCommand({ TableName: tableName }));
    if (!resp.TableDescription) throw new Error('TableDescription to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'CreateTable_CompositeKey', async () => {
    const resp = await client.send(new CreateTableCommand({
      TableName: ctx.compTableName,
      AttributeDefinitions: [
        { AttributeName: 'pk', AttributeType: 'S' },
        { AttributeName: 'sk', AttributeType: 'S' },
      ],
      KeySchema: [
        { AttributeName: 'pk', KeyType: 'HASH' },
        { AttributeName: 'sk', KeyType: 'RANGE' },
      ],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    if (!resp.TableDescription) throw new Error('TableDescription to be defined');
    if (!resp.TableDescription.KeySchema || resp.TableDescription.KeySchema.length !== 2) {
      throw new Error('expected 2 key schema elements');
    }
  }));

  const compItems: Array<Record<string, import('@aws-sdk/client-dynamodb').AttributeValue>> = [
    { pk: { S: 'user1' }, sk: { S: 'meta' }, name: { S: 'Alice' }, age: { N: '30' }, active: { BOOL: true } },
    { pk: { S: 'user1' }, sk: { S: 'order1' }, amount: { N: '100' }, status: { S: 'shipped' } },
    { pk: { S: 'user1' }, sk: { S: 'order2' }, amount: { N: '200' }, status: { S: 'pending' } },
    { pk: { S: 'user1' }, sk: { S: 'order3' }, amount: { N: '50' }, status: { S: 'delivered' } },
    { pk: { S: 'user2' }, sk: { S: 'meta' }, name: { S: 'Bob' }, age: { N: '25' }, active: { BOOL: false } },
    { pk: { S: 'user2' }, sk: { S: 'order1' }, amount: { N: '300' }, status: { S: 'shipped' } },
  ];
  for (const item of compItems) {
    await client.send(new PutItemCommand({ TableName: ctx.compTableName, Item: item }));
  }

  results.push(await runner.runTest('dynamodb', 'CreateTable_Validation_NoKeySchema', async () => {
    try {
      await client.send(new CreateTableCommand({ TableName: 'BadTable' }));
      throw new Error('expected error for missing KeySchema');
    } catch (err) {
      assertErrorContains(err, 'ValidationException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteTable_Protected', async () => {
    const dpTable = `DP-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: dpTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
      DeletionProtectionEnabled: true,
    }));
    try {
      try {
        await client.send(new DeleteTableCommand({ TableName: dpTable }));
        throw new Error('expected ResourceInUseException for protected table');
      } catch (err) {
        assertErrorContains(err, 'ResourceInUseException');
      }
    } finally {
      await client.send(new UpdateTableCommand({
        TableName: dpTable,
        DeletionProtectionEnabled: false,
      }));
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: dpTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'CreateTable_GSI', async () => {
    const gsiTable = `GSITable-${ts}`;
    const resp = await client.send(new CreateTableCommand({
      TableName: gsiTable,
      AttributeDefinitions: [
        { AttributeName: 'pk', AttributeType: 'S' },
        { AttributeName: 'sk', AttributeType: 'S' },
        { AttributeName: 'gsi_pk', AttributeType: 'S' },
      ],
      KeySchema: [
        { AttributeName: 'pk', KeyType: 'HASH' },
        { AttributeName: 'sk', KeyType: 'RANGE' },
      ],
      BillingMode: 'PAY_PER_REQUEST',
      GlobalSecondaryIndexes: [{
        IndexName: 'gsi1',
        KeySchema: [
          { AttributeName: 'gsi_pk', KeyType: 'HASH' },
          { AttributeName: 'sk', KeyType: 'RANGE' },
        ],
        Projection: { ProjectionType: 'ALL' },
      }],
    }));
    try {
      if (!resp.TableDescription) throw new Error('TableDescription to be defined');
      if (!resp.TableDescription.GlobalSecondaryIndexes || resp.TableDescription.GlobalSecondaryIndexes.length !== 1) {
        throw new Error('expected 1 GSI in description');
      }
      if (resp.TableDescription.GlobalSecondaryIndexes[0].IndexName !== 'gsi1') {
        throw new Error(`expected GSI name 'gsi1', got ${resp.TableDescription.GlobalSecondaryIndexes[0].IndexName}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: gsiTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'CreateTable_StreamSpec', async () => {
    const stTable = `StreamTable-${ts}`;
    const resp = await client.send(new CreateTableCommand({
      TableName: stTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
      StreamSpecification: {
        StreamEnabled: true,
        StreamViewType: 'NEW_IMAGE',
      },
    }));
    try {
      if (!resp.TableDescription) throw new Error('TableDescription to be defined');
      const spec = resp.TableDescription.StreamSpecification;
      if (!spec || !spec.StreamEnabled) throw new Error('expected StreamEnabled=true');
      if (!resp.TableDescription.LatestStreamArn) throw new Error('expected LatestStreamArn');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: stTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateTable_EnableSSE', async () => {
    const sseTable = `SSETable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: sseTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new UpdateTableCommand({
        TableName: sseTable,
        SSESpecification: { Enabled: true, SSEType: 'AES256' },
      }));
      if (!resp.TableDescription) throw new Error('TableDescription to be defined');
      if (!resp.TableDescription.SSEDescription) throw new Error('expected SSEDescription');
      if (resp.TableDescription.SSEDescription.Status !== 'ENABLED') {
        throw new Error(`expected SSEStatus=ENABLED, got ${resp.TableDescription.SSEDescription.Status}`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: sseTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateTable_AddGSI', async () => {
    const agTable = `AddGSI-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: agTable,
      AttributeDefinitions: [
        { AttributeName: 'id', AttributeType: 'S' },
        { AttributeName: 'gsi_pk', AttributeType: 'S' },
      ],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new UpdateTableCommand({
        TableName: agTable,
        GlobalSecondaryIndexUpdates: [{
          Create: {
            IndexName: 'new_gsi',
            KeySchema: [{ AttributeName: 'gsi_pk', KeyType: 'HASH' }],
            Projection: { ProjectionType: 'ALL' },
          },
        }],
      }));
      if (!resp.TableDescription) throw new Error('TableDescription to be defined');
      if (!resp.TableDescription.GlobalSecondaryIndexes || resp.TableDescription.GlobalSecondaryIndexes.length !== 1) {
        throw new Error('expected 1 GSI after update');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: agTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateTimeToLive_Disable', async () => {
    const ttlTable = `TTLDis-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ttlTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new UpdateTimeToLiveCommand({
        TableName: ttlTable,
        TimeToLiveSpecification: { AttributeName: 'ttl', Enabled: true },
      }));
      const resp = await client.send(new UpdateTimeToLiveCommand({
        TableName: ttlTable,
        TimeToLiveSpecification: { AttributeName: 'ttl', Enabled: false },
      }));
      if (!resp.TimeToLiveSpecification) throw new Error('TimeToLiveSpecification to be defined');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ttlTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'CreateTable_DuplicateName', async () => {
    const dupTable = `DupTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: dupTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      try {
        await client.send(new CreateTableCommand({
          TableName: dupTable,
          AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
          KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
          BillingMode: 'PAY_PER_REQUEST',
        }));
        throw new Error('expected error for duplicate table name');
      } catch (err) {
        assertErrorContains(err, 'ResourceInUseException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: dupTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'ListGlobalTables_Pagination', async () => {
    const pagGT1 = `PagGT-${ts}-1`;
    const pagGT2 = `PagGT-${ts}-2`;
    await client.send(new CreateGlobalTableCommand({
      GlobalTableName: pagGT1,
      ReplicationGroup: [{ RegionName: 'us-east-1' }],
    }));
    try {
      await client.send(new CreateGlobalTableCommand({
        GlobalTableName: pagGT2,
        ReplicationGroup: [{ RegionName: 'us-east-1' }],
      }));
      try {
        const found: Record<string, boolean> = { [pagGT1]: false, [pagGT2]: false };
        let exclusiveStartName: string | undefined;
        let pageCount = 0;
        do {
          const resp = await client.send(new ListGlobalTablesCommand({
            Limit: 1,
            ExclusiveStartGlobalTableName: exclusiveStartName,
          }));
          pageCount++;
          for (const gt of resp.GlobalTables ?? []) {
            if (gt.GlobalTableName && gt.GlobalTableName in found) {
              found[gt.GlobalTableName] = true;
            }
          }
          exclusiveStartName = resp.LastEvaluatedGlobalTableName;
        } while (exclusiveStartName);
        for (const [name, f] of Object.entries(found)) {
          if (!f) throw new Error(`created global table "${name}" not found in ListGlobalTables`);
        }
        if (pageCount < 2) throw new Error(`expected at least 2 pages, got ${pageCount}`);
      } finally {
        await safeCleanup(async () => {
          await client.send(new DeleteTableCommand({ TableName: pagGT2 }));
        });
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: pagGT1 }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'GetItem_NonExistentTable', async () => {
    try {
      await client.send(new GetItemCommand({ TableName: 'NoSuchTable_xyz', Key: { id: { S: 'k' } } }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'PutItem_NonExistentTable', async () => {
    try {
      await client.send(new PutItemCommand({ TableName: 'NoSuchTable_xyz', Item: { id: { S: 'k' } } }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Query_NonExistentTable', async () => {
    try {
      await client.send(new QueryCommand({
        TableName: 'NoSuchTable_xyz',
        KeyConditionExpression: 'id = :id',
        ExpressionAttributeValues: { ':id': { S: 'k' } },
      }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Scan_NonExistentTable', async () => {
    try {
      await client.send(new ScanCommand({ TableName: 'NoSuchTable_xyz' }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'DescribeTable_NonExistentTable', async () => {
    try {
      await client.send(new DescribeTableCommand({ TableName: 'NoSuchTable_xyz' }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteTable_NonExistentTable', async () => {
    try {
      await client.send(new DeleteTableCommand({ TableName: 'NoSuchTable_xyz' }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'BatchGetItem_NonExistentTable', async () => {
    try {
      await client.send(new BatchGetItemCommand({
        RequestItems: {
          NonExistentTable_xyz: {
            Keys: [{ id: { S: 'k' } }],
          },
        },
      }));
      throw new Error('expected error for non-existent table');
    } catch (err) {
      assertErrorContains(err, 'ResourceNotFoundException');
    }
  }));

  return results;
}
