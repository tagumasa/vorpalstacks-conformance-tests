import {
  CreateTableCommand,
  DeleteTableCommand,
  PutItemCommand,
  QueryCommand,
  ScanCommand,
  GetItemCommand,
  UpdateItemCommand,
} from '@aws-sdk/client-dynamodb';
import { DynamoDBTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runQueryTests(ctx: DynamoDBTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, tableName, compTableName } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'Query', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: tableName,
      KeyConditionExpression: 'id = :id',
      ExpressionAttributeValues: { ':id': { S: 'test1' } },
    }));
    if (!resp.Count || resp.Count === 0) throw new Error('no items found');
  }));

  results.push(await runner.runTest('dynamodb', 'Scan', async () => {
    const resp = await client.send(new ScanCommand({ TableName: tableName }));
    if (!resp.Count || resp.Count === 0) throw new Error('no items found');
  }));

  results.push(await runner.runTest('dynamodb', 'Query_CompositeKey', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk',
      ExpressionAttributeValues: { ':pk': { S: 'user1' } },
    }));
    if (resp.Count !== 4) throw new Error(`expected 4 items for user1, got ${resp.Count}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_SortKeyCondition', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk AND sk = :sk',
      ExpressionAttributeValues: { ':pk': { S: 'user1' }, ':sk': { S: 'order2' } },
    }));
    if (resp.Count !== 1) throw new Error(`expected 1 item, got ${resp.Count}`);
    const amt = resp.Items?.[0]?.['amount'];
    if (!amt || !amt.N || amt.N !== '200') throw new Error(`expected amount=200, got ${amt?.N}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_SortKeyBeginsWith', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk AND begins_with(sk, :prefix)',
      ExpressionAttributeValues: { ':pk': { S: 'user1' }, ':prefix': { S: 'order' } },
    }));
    if (resp.Count !== 3) throw new Error(`expected 3 order items, got ${resp.Count}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_SortKeyBetween', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk AND sk BETWEEN :low AND :high',
      ExpressionAttributeValues: { ':pk': { S: 'user1' }, ':low': { S: 'order1' }, ':high': { S: 'order3' } },
    }));
    if (resp.Count !== 3) throw new Error(`expected 3 items in BETWEEN, got ${resp.Count}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_ScanIndexForward', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk',
      ExpressionAttributeValues: { ':pk': { S: 'user1' } },
      ScanIndexForward: false,
    }));
    if (resp.Count !== 4) throw new Error(`expected 4 items, got ${resp.Count}`);
    const firstSK = resp.Items?.[0]?.['sk'];
    if (!firstSK || !firstSK.S || firstSK.S !== 'order3') throw new Error(`expected first item sk=order3 in descending order, got ${firstSK?.S}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_FilterExpression', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk AND begins_with(sk, :prefix)',
      FilterExpression: '#s = :status',
      ExpressionAttributeNames: { '#s': 'status' },
      ExpressionAttributeValues: { ':pk': { S: 'user1' }, ':prefix': { S: 'order' }, ':status': { S: 'shipped' } },
    }));
    if (resp.Count !== 1) throw new Error(`expected 1 shipped order, got ${resp.Count}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_Limit', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk',
      ExpressionAttributeValues: { ':pk': { S: 'user1' } },
      Limit: 2,
    }));
    if (!resp.Count || resp.Count > 2) throw new Error(`expected at most 2 items with Limit=2, got ${resp.Count}`);
    if (!resp.LastEvaluatedKey) throw new Error('expected LastEvaluatedKey when Limit < total items');
  }));

  results.push(await runner.runTest('dynamodb', 'Query_ProjectionExpression', async () => {
    const resp = await client.send(new QueryCommand({
      TableName: compTableName,
      KeyConditionExpression: 'pk = :pk AND sk = :sk',
      ProjectionExpression: 'pk, sk, amount',
      ExpressionAttributeValues: { ':pk': { S: 'user1' }, ':sk': { S: 'order1' } },
    }));
    if (resp.Count !== 1) throw new Error(`expected 1 item, got ${resp.Count}`);
    if (Object.keys(resp.Items?.[0] ?? {}).length !== 3) throw new Error(`expected 3 projected attributes, got ${Object.keys(resp.Items?.[0] ?? {}).length}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Scan_FilterExpression', async () => {
    const resp = await client.send(new ScanCommand({
      TableName: compTableName,
      FilterExpression: '#s = :status',
      ExpressionAttributeNames: { '#s': 'status' },
      ExpressionAttributeValues: { ':status': { S: 'shipped' } },
    }));
    if (resp.Count !== 2) throw new Error(`expected 2 shipped items, got ${resp.Count}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Scan_ProjectionExpression', async () => {
    const resp = await client.send(new ScanCommand({
      TableName: compTableName,
      ProjectionExpression: 'pk, name',
    }));
    for (const item of resp.Items ?? []) {
      if (!item['pk']) throw new Error("expected 'pk' in projected item");
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Scan_Limit', async () => {
    const resp = await client.send(new ScanCommand({
      TableName: compTableName,
      Limit: 3,
    }));
    if (!resp.Count || resp.Count > 3) throw new Error(`expected at most 3 items with Limit=3, got ${resp.Count}`);
  }));

  results.push(await runner.runTest('dynamodb', 'Query_ReturnConsumedCapacity', async () => {
    const qTable = `QCapTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: qTable,
      AttributeDefinitions: [{ AttributeName: 'pk', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'pk', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: qTable,
        Item: { pk: { S: 'key1' } },
      }));
      const resp = await client.send(new QueryCommand({
        TableName: qTable,
        KeyConditionExpression: 'pk = :pk',
        ExpressionAttributeValues: { ':pk': { S: 'key1' } },
        ReturnConsumedCapacity: 'TOTAL',
      }));
      if (!resp.ConsumedCapacity) throw new Error('expected ConsumedCapacity in response');
      if (resp.ConsumedCapacity.TableName !== qTable) throw new Error(`ConsumedCapacity.TableName mismatch, got ${resp.ConsumedCapacity.TableName}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: qTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Query_Limit_Pagination', async () => {
    const pagTableName = `PagTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: pagTableName,
      AttributeDefinitions: [{ AttributeName: 'pk', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'pk', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      for (const _ of [0, 1, 2, 3, 4]) {
        await client.send(new PutItemCommand({
          TableName: pagTableName,
          Item: { pk: { S: `item-${_}` }, data: { S: 'pagination' } },
        }));
      }
      const allItems: string[] = [];
      let exclusiveStartKey: Record<string, import('@aws-sdk/client-dynamodb').AttributeValue> | undefined;
      do {
        const resp = await client.send(new QueryCommand({
          TableName: pagTableName,
          KeyConditionExpression: 'pk = :pk',
          ExpressionAttributeValues: { ':pk': { S: 'item-0' } },
          Limit: 2,
          ExclusiveStartKey: exclusiveStartKey,
        }));
        for (const item of resp.Items ?? []) {
          const pk = item['pk'];
          if (pk?.S) allItems.push(pk.S);
        }
        exclusiveStartKey = resp.LastEvaluatedKey;
      } while (exclusiveStartKey);
      if (allItems.length !== 1) throw new Error(`expected 1 item for pk=item-0, got ${allItems.length}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: pagTableName }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Condition_AttributeExists_True', async () => {
    const ceTable = `CE-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ceTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: ceTable,
        Item: { id: { S: 'ce1' }, name: { S: 'Test' } },
      }));
      await client.send(new UpdateItemCommand({
        TableName: ceTable,
        Key: { id: { S: 'ce1' } },
        UpdateExpression: 'SET #s = :v',
        ConditionExpression: 'attribute_exists(name)',
        ExpressionAttributeNames: { '#s': 'status' },
        ExpressionAttributeValues: { ':v': { S: 'active' } },
      }));
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ceTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Condition_AttributeNotExists_False', async () => {
    const ceTable = `CENE-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ceTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: ceTable,
        Item: { id: { S: 'cene1' }, name: { S: 'Test' } },
      }));
      try {
        await client.send(new UpdateItemCommand({
          TableName: ceTable,
          Key: { id: { S: 'cene1' } },
          UpdateExpression: 'SET #s = :v',
          ConditionExpression: 'attribute_not_exists(name)',
          ExpressionAttributeNames: { '#s': 'status' },
          ExpressionAttributeValues: { ':v': { S: 'active' } },
        }));
        throw new Error('expected ConditionalCheckFailedException');
      } catch (err) {
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ceTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Condition_BeginsWith', async () => {
    const bwTable = `BW-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: bwTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: bwTable,
        Item: { id: { S: 'bw1' }, name: { S: 'HelloWorld' } },
      }));
      await client.send(new UpdateItemCommand({
        TableName: bwTable,
        Key: { id: { S: 'bw1' } },
        UpdateExpression: 'SET #s = :v',
        ConditionExpression: 'begins_with(name, :prefix)',
        ExpressionAttributeNames: { '#s': 'status' },
        ExpressionAttributeValues: { ':v': { S: 'matched' }, ':prefix': { S: 'Hello' } },
      }));
      try {
        await client.send(new UpdateItemCommand({
          TableName: bwTable,
          Key: { id: { S: 'bw1' } },
          UpdateExpression: 'SET #s = :v',
          ConditionExpression: 'begins_with(name, :prefix)',
          ExpressionAttributeNames: { '#s': 'status' },
          ExpressionAttributeValues: { ':v': { S: 'nope' }, ':prefix': { S: 'XYZ' } },
        }));
        throw new Error('expected ConditionalCheckFailedException for non-matching begins_with');
      } catch (err) {
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: bwTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Condition_Contains', async () => {
    const ctTable = `CT-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ctTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: ctTable,
        Item: { id: { S: 'ct1' }, tags: { SS: ['go', 'java', 'python'] } },
      }));
      await client.send(new UpdateItemCommand({
        TableName: ctTable,
        Key: { id: { S: 'ct1' } },
        UpdateExpression: 'SET #s = :v',
        ConditionExpression: 'contains(tags, :tag)',
        ExpressionAttributeNames: { '#s': 'status' },
        ExpressionAttributeValues: { ':v': { S: 'matched' }, ':tag': { S: 'java' } },
      }));
      try {
        await client.send(new UpdateItemCommand({
          TableName: ctTable,
          Key: { id: { S: 'ct1' } },
          UpdateExpression: 'SET #s = :v',
          ConditionExpression: 'contains(tags, :tag)',
          ExpressionAttributeNames: { '#s': 'status' },
          ExpressionAttributeValues: { ':v': { S: 'nope' }, ':tag': { S: 'rust' } },
        }));
        throw new Error('expected ConditionalCheckFailedException for non-matching contains');
      } catch (err) {
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ctTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Condition_ComparisonOperators', async () => {
    const coTable = `CO-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: coTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: coTable,
        Item: { id: { S: 'co1' }, val: { N: '10' } },
      }));
      const cases: Array<{ cond: string; val: string; pass: boolean }> = [
        { cond: '#v = :x', val: '10', pass: true },
        { cond: '#v <> :x', val: '20', pass: true },
        { cond: '#v < :x', val: '20', pass: true },
        { cond: '#v <= :x', val: '10', pass: true },
        { cond: '#v > :x', val: '5', pass: true },
        { cond: '#v >= :x', val: '10', pass: true },
        { cond: '#v < :x', val: '5', pass: false },
        { cond: '#v > :x', val: '20', pass: false },
      ];
      for (const tc of cases) {
        try {
          await client.send(new UpdateItemCommand({
            TableName: coTable,
            Key: { id: { S: 'co1' } },
            UpdateExpression: 'SET #s = :s',
            ConditionExpression: tc.cond,
            ExpressionAttributeNames: { '#v': 'val', '#s': 'status' },
            ExpressionAttributeValues: { ':s': { S: 'ok' }, ':x': { N: tc.val } },
          }));
          if (!tc.pass) throw new Error(`condition '${tc.cond}' with val '${tc.val}' should fail`);
        } catch (err) {
          if (tc.pass) throw new Error(`condition '${tc.cond}' with val '${tc.val}' should pass: ${err}`);
        }
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: coTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'Condition_AND_OR', async () => {
    const aoTable = `AO-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: aoTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: aoTable,
        Item: { id: { S: 'ao1' }, val: { N: '10' }, active: { BOOL: true } },
      }));
      await client.send(new UpdateItemCommand({
        TableName: aoTable,
        Key: { id: { S: 'ao1' } },
        UpdateExpression: 'SET #s = :v',
        ConditionExpression: 'active = :t AND #v > :x',
        ExpressionAttributeNames: { '#s': 'status', '#v': 'val' },
        ExpressionAttributeValues: { ':v': { S: 'and-pass' }, ':t': { BOOL: true }, ':x': { N: '5' } },
      }));
      try {
        await client.send(new UpdateItemCommand({
          TableName: aoTable,
          Key: { id: { S: 'ao1' } },
          UpdateExpression: 'SET #s = :v',
          ConditionExpression: 'active = :f AND #v > :x',
          ExpressionAttributeNames: { '#s': 'status', '#v': 'val' },
          ExpressionAttributeValues: { ':v': { S: 'and-fail' }, ':f': { BOOL: false }, ':x': { N: '5' } },
        }));
        throw new Error('AND condition (one false) should fail');
      } catch (err) {
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
      await client.send(new UpdateItemCommand({
        TableName: aoTable,
        Key: { id: { S: 'ao1' } },
        UpdateExpression: 'SET #s = :v',
        ConditionExpression: 'active = :f OR #v > :x',
        ExpressionAttributeNames: { '#s': 'status', '#v': 'val' },
        ExpressionAttributeValues: { ':v': { S: 'or-pass' }, ':f': { BOOL: false }, ':x': { N: '5' } },
      }));
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: aoTable }));
      });
    }
  }));

  return results;
}
