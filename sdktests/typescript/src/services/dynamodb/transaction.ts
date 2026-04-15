import {
  CreateTableCommand,
  DeleteTableCommand,
  PutItemCommand,
  GetItemCommand,
  TransactWriteItemsCommand,
  TransactGetItemsCommand,
  ExecuteTransactionCommand,
} from '@aws-sdk/client-dynamodb';
import { DynamoDBTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runTransactionTests(ctx: DynamoDBTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, tableName, compTableName } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'TransactWriteItems', async () => {
    const resp = await client.send(new TransactWriteItemsCommand({
      TransactItems: [{
        Put: {
          TableName: tableName,
          Item: { id: { S: 'transact1' }, name: { S: 'Transact Item' } },
        },
      }],
    }));
    if (!resp) throw new Error('TransactWriteItems response to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'TransactGetItems', async () => {
    const resp = await client.send(new TransactGetItemsCommand({
      TransactItems: [{
        Get: {
          TableName: tableName,
          Key: { id: { S: 'transact1' } },
        },
      }],
    }));
    if (!resp.Responses || resp.Responses.length === 0) throw new Error('no responses');
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteTransaction', async () => {
    const resp = await client.send(new ExecuteTransactionCommand({
      TransactStatements: [{
        Statement: `SELECT * FROM "${tableName}" WHERE id = 'transact1'`,
      }],
    }));
    if (!resp.Responses || resp.Responses.length === 0) throw new Error('no responses');
  }));

  results.push(await runner.runTest('dynamodb', 'TransactWriteItems_MultipleOps', async () => {
    const twTable = `TW-Multi-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: twTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: twTable,
        Item: { id: { S: 't1' }, val: { N: '10' } },
      }));
      await client.send(new TransactWriteItemsCommand({
        TransactItems: [
          {
            Update: {
              TableName: twTable,
              Key: { id: { S: 't1' } },
              UpdateExpression: 'SET #v = #v + :inc',
              ExpressionAttributeNames: { '#v': 'val' },
              ExpressionAttributeValues: { ':inc': { N: '5' } },
            },
          },
          {
            Put: {
              TableName: twTable,
              Item: { id: { S: 't2' }, val: { N: '42' } },
            },
          },
          {
            ConditionCheck: {
              TableName: twTable,
              Key: { id: { S: 't1' } },
              ConditionExpression: 'attribute_exists(id)',
            },
          },
        ],
      }));
      const resp = await client.send(new GetItemCommand({ TableName: twTable, Key: { id: { S: 't1' } } }));
      const val = resp.Item?.['val'];
      if (!val || !val.N || val.N !== '15') throw new Error(`expected val=15 after transact update, got ${val?.N}`);
      const resp2 = await client.send(new GetItemCommand({ TableName: twTable, Key: { id: { S: 't2' } } }));
      if (!resp2.Item) throw new Error('expected t2 to be created');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: twTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'TransactWriteItems_ConditionFail', async () => {
    const twTable = `TW-CondF-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: twTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: twTable,
        Item: { id: { S: 'nofail' }, val: { N: '1' } },
      }));
      try {
        await client.send(new TransactWriteItemsCommand({
          TransactItems: [{
            Update: {
              TableName: twTable,
              Key: { id: { S: 'nofail' } },
              UpdateExpression: 'SET #v = :x',
              ConditionExpression: '#v = :expect',
              ExpressionAttributeNames: { '#v': 'val' },
              ExpressionAttributeValues: { ':x': { N: '2' }, ':expect': { N: '999' } },
            },
          }],
        }));
        throw new Error('expected TransactionCanceledException');
      } catch (err) {
        assertErrorContains(err, 'TransactionCanceledException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: twTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'TransactWriteItems_Delete', async () => {
    const twTable = `TW-Del-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: twTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: twTable,
        Item: { id: { S: 'td1' }, name: { S: 'ToDelete' } },
      }));
      await client.send(new TransactWriteItemsCommand({
        TransactItems: [{
          Delete: {
            TableName: twTable,
            Key: { id: { S: 'td1' } },
          },
        }],
      }));
      const resp = await client.send(new GetItemCommand({ TableName: twTable, Key: { id: { S: 'td1' } } }));
      if (resp.Item && Object.keys(resp.Item).length !== 0) throw new Error('item should be deleted after TransactWriteItems Delete');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: twTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'TransactGetItems_NonExistentKey', async () => {
    const tgTable = `TG-NE-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: tgTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new TransactGetItemsCommand({
        TransactItems: [{
          Get: {
            TableName: tgTable,
            Key: { id: { S: 'nonexistent' } },
          },
        }],
      }));
      if (!resp.Responses || resp.Responses.length !== 1) throw new Error(`expected 1 response, got ${resp.Responses?.length}`);
      if (resp.Responses[0].Item) throw new Error('expected nil Item for non-existent key');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: tgTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteTransaction_MixedReadWrite', async () => {
    try {
      await client.send(new ExecuteTransactionCommand({
        TransactStatements: [
          { Statement: `SELECT * FROM "${compTableName}" WHERE pk = 'user1'` },
          { Statement: `INSERT INTO "${compTableName}" VALUE {'pk': 'user3', 'sk': 'meta', 'name': 'Charlie'}` },
        ],
      }));
      throw new Error('expected TransactionConflictException for mixed read/write');
    } catch (err) {
      assertErrorContains(err, 'TransactionConflictException');
    }
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteTransaction_WriteOnly', async () => {
    const etTable = `ETWrite-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: etTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new ExecuteTransactionCommand({
        TransactStatements: [
          { Statement: `INSERT INTO "${etTable}" VALUE {'id': 'et1', 'val': 'hello'}` },
          { Statement: `INSERT INTO "${etTable}" VALUE {'id': 'et2', 'val': 'world'}` },
        ],
      }));
      const resp = await client.send(new GetItemCommand({ TableName: etTable, Key: { id: { S: 'et1' } } }));
      if (!resp.Item) throw new Error('expected et1 to be created');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: etTable }));
      });
    }
  }));

  return results;
}
