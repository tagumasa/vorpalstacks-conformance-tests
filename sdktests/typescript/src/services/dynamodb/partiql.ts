import {
  CreateTableCommand,
  DeleteTableCommand,
  PutItemCommand,
  GetItemCommand,
  ExecuteStatementCommand,
} from '@aws-sdk/client-dynamodb';
import { DynamoDBTestContext } from './context.js';
import { safeCleanup } from '../../helpers.js';

export async function runPartiQLTests(ctx: DynamoDBTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, tableName, compTableName } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'ExecuteStatement (PartiQL)', async () => {
    const resp = await client.send(new ExecuteStatementCommand({
      Statement: `INSERT INTO "${tableName}" VALUE {'id': 'partiql1', 'name': 'PartiQL Item'}`,
    }));
    if (!resp) throw new Error('ExecuteStatement response to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteStatement (SELECT)', async () => {
    const resp = await client.send(new ExecuteStatementCommand({
      Statement: `SELECT * FROM "${tableName}" WHERE id = 'partiql1'`,
    }));
    if (!resp.Items || resp.Items.length === 0) throw new Error('no items found');
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteStatement_SelectWhere', async () => {
    const resp = await client.send(new ExecuteStatementCommand({
      Statement: `SELECT * FROM "${compTableName}" WHERE pk = 'user1' AND sk = 'order2'`,
    }));
    if (!resp.Items || resp.Items.length !== 1) throw new Error(`expected 1 item, got ${resp.Items?.length}`);
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteStatement_Update', async () => {
    const puTable = `PQUpd-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: puTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: puTable,
        Item: { id: { S: 'pu1' }, val: { N: '10' } },
      }));
      await client.send(new ExecuteStatementCommand({
        Statement: `UPDATE "${puTable}" SET val = 20 WHERE id = 'pu1'`,
      }));
      const getResp = await client.send(new GetItemCommand({
        TableName: puTable,
        Key: { id: { S: 'pu1' } },
      }));
      const val = getResp.Item?.['val'];
      if (!val || !val.N || val.N !== '20') throw new Error(`expected val=20 after PartiQL UPDATE, got ${val?.N}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: puTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'ExecuteStatement_Delete', async () => {
    const pdTable = `PQDel-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: pdTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: pdTable,
        Item: { id: { S: 'pd1' }, val: { N: '99' } },
      }));
      await client.send(new ExecuteStatementCommand({
        Statement: `DELETE FROM "${pdTable}" WHERE id = 'pd1'`,
      }));
      const getResp = await client.send(new GetItemCommand({
        TableName: pdTable,
        Key: { id: { S: 'pd1' } },
      }));
      if (getResp.Item && Object.keys(getResp.Item).length !== 0) throw new Error('item should be deleted after PartiQL DELETE');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: pdTable }));
      });
    }
  }));

  return results;
}
