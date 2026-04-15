import {
  CreateTableCommand,
  DeleteTableCommand,
  DeleteItemCommand,
  PutItemCommand,
  GetItemCommand,
  BatchWriteItemCommand,
  BatchGetItemCommand,
  BatchExecuteStatementCommand,
} from '@aws-sdk/client-dynamodb';
import { DynamoDBTestContext } from './context.js';
import { safeCleanup } from '../../helpers.js';

export async function runBatchTests(ctx: DynamoDBTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, tableName } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'BatchWriteItem', async () => {
    const resp = await client.send(new BatchWriteItemCommand({
      RequestItems: {
        [tableName]: [
          { PutRequest: { Item: { id: { S: 'batch1' }, data: { S: 'batch item 1' } } } },
          { PutRequest: { Item: { id: { S: 'batch2' }, data: { S: 'batch item 2' } } } },
        ],
      },
    }));
    if (!resp.UnprocessedItems) throw new Error('UnprocessedItems to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'BatchGetItem', async () => {
    const resp = await client.send(new BatchGetItemCommand({
      RequestItems: {
        [tableName]: {
          Keys: [
            { id: { S: 'batch1' } },
            { id: { S: 'batch2' } },
          ],
        },
      },
    }));
    if (!resp.Responses || Object.keys(resp.Responses).length === 0) throw new Error('no responses');
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteItem', async () => {
    await client.send(new DeleteItemCommand({
      TableName: tableName,
      Key: { id: { S: 'test1' } },
    }));
  }));

  results.push(await runner.runTest('dynamodb', 'BatchExecuteStatement', async () => {
    const resp = await client.send(new BatchExecuteStatementCommand({
      Statements: [{
        Statement: `UPDATE "${tableName}" SET #n = :name WHERE id = 'batch1'`,
        Parameters: [{ S: 'Updated via Batch' }],
      }],
    }));
    if (!resp.Responses) throw new Error('BatchExecuteStatement Responses to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'BatchWriteItem_DeleteRequest', async () => {
    const bwTable = `BWDel-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: bwTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: bwTable,
        Item: { id: { S: 'del1' }, name: { S: 'ToDelete' } },
      }));
      await client.send(new BatchWriteItemCommand({
        RequestItems: {
          [bwTable]: [{
            DeleteRequest: { Key: { id: { S: 'del1' } } },
          }],
        },
      }));
      const resp = await client.send(new GetItemCommand({
        TableName: bwTable,
        Key: { id: { S: 'del1' } },
      }));
      if (resp.Item && Object.keys(resp.Item).length !== 0) throw new Error('item should be deleted after BatchWriteItem DeleteRequest');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: bwTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'BatchGetItem_Projection', async () => {
    const bgTable = `BGProj-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: bgTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: bgTable,
        Item: { id: { S: 'bp1' }, name: { S: 'Alice' }, email: { S: 'alice@test.com' } },
      }));
      const resp = await client.send(new BatchGetItemCommand({
        RequestItems: {
          [bgTable]: {
            Keys: [{ id: { S: 'bp1' } }],
            ProjectionExpression: 'id, name',
          },
        },
      }));
      const items = resp.Responses?.[bgTable] ?? [];
      if (items.length !== 1) throw new Error(`expected 1 item, got ${items.length}`);
      if (Object.keys(items[0]).length !== 2) throw new Error(`expected 2 projected attributes, got ${Object.keys(items[0]).length}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: bgTable }));
      });
    }
  }));

  return results;
}
