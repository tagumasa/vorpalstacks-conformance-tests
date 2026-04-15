import {
  CreateTableCommand,
  DeleteTableCommand,
  PutItemCommand,
  GetItemCommand,
  UpdateItemCommand,
  DeleteItemCommand,
} from '@aws-sdk/client-dynamodb';
import { DynamoDBTestContext } from './context.js';
import { assertErrorContains, safeCleanup } from '../../helpers.js';

export async function runItemTests(ctx: DynamoDBTestContext, runner: import('../../runner.js').TestRunner): Promise<import('../../runner.js').TestResult[]> {
  const { client, ts, tableName } = ctx;
  const results: import('../../runner.js').TestResult[] = [];

  results.push(await runner.runTest('dynamodb', 'PutItem', async () => {
    const resp = await client.send(new PutItemCommand({
      TableName: tableName,
      Item: {
        id: { S: 'test1' },
        name: { S: 'Test Item' },
        count: { N: '42' },
      },
    }));
    if (!resp) throw new Error('PutItem response to be defined');
  }));

  results.push(await runner.runTest('dynamodb', 'GetItem', async () => {
    const resp = await client.send(new GetItemCommand({
      TableName: tableName,
      Key: { id: { S: 'test1' } },
    }));
    if (!resp.Item) throw new Error('item not found');
    const name = resp.Item['name'];
    if (!name || !name.S || name.S !== 'Test Item') throw new Error('item name mismatch');
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem', async () => {
    const resp = await client.send(new UpdateItemCommand({
      TableName: tableName,
      Key: { id: { S: 'test1' } },
      UpdateExpression: 'SET #n = :name',
      ExpressionAttributeNames: { '#n': 'name' },
      ExpressionAttributeValues: { ':name': { S: 'Updated' } },
      ReturnValues: 'ALL_NEW',
    }));
    if (!resp.Attributes) throw new Error('attributes not found');
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_ConditionalCheckFail', async () => {
    const errTable = `CondTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: errTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: errTable,
        Item: { id: { S: 'cond1' }, status: { S: 'active' } },
      }));
      try {
        await client.send(new UpdateItemCommand({
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
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: errTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'GetItem_NonExistentKey', async () => {
    const errTable = `GetItemErr-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: errTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new GetItemCommand({
        TableName: errTable,
        Key: { id: { S: 'nonexistent' } },
      }));
      if (resp.Item && Object.keys(resp.Item).length !== 0) {
        throw new Error(`expected empty item for non-existent key, got ${Object.keys(resp.Item).length} attributes`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: errTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteItem_ConditionalCheckFail', async () => {
    const errTable = `DelCondTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: errTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: errTable,
        Item: { id: { S: 'del1' }, status: { S: 'active' } },
      }));
      try {
        await client.send(new DeleteItemCommand({
          TableName: errTable,
          Key: { id: { S: 'del1' } },
          ConditionExpression: 'attribute_not_exists(id)',
        }));
        throw new Error('expected ConditionalCheckFailedException');
      } catch (err) {
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: errTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'PutItem_ConditionPass', async () => {
    const condTable = `CondPut-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: condTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: condTable,
        Item: { id: { S: 'cp1' }, val: { N: '10' } },
        ConditionExpression: 'attribute_not_exists(id)',
      }));
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: condTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'PutItem_ConditionFail', async () => {
    const condTable = `CondPutF-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: condTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: condTable,
        Item: { id: { S: 'cpf1' }, val: { N: '10' } },
      }));
      try {
        await client.send(new PutItemCommand({
          TableName: condTable,
          Item: { id: { S: 'cpf1' }, val: { N: '20' } },
          ConditionExpression: 'attribute_not_exists(id)',
        }));
        throw new Error('expected ConditionalCheckFailedException');
      } catch (err) {
        assertErrorContains(err, 'ConditionalCheckFailedException');
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: condTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'PutItem_ReturnValues', async () => {
    const rvTable = `RVTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: rvTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new PutItemCommand({
        TableName: rvTable,
        Item: { id: { S: 'rv1' }, name: { S: 'Alice' }, count: { N: '10' } },
        ReturnValues: 'ALL_OLD',
      }));
      if (resp.Attributes) {
        throw new Error('first PutItem with ReturnValues=ALL_OLD should have nil Attributes for new item');
      }

      const resp2 = await client.send(new PutItemCommand({
        TableName: rvTable,
        Item: { id: { S: 'rv1' }, name: { S: 'Bob' }, count: { N: '20' } },
        ReturnValues: 'ALL_OLD',
      }));
      if (!resp2.Attributes) throw new Error('second PutItem with ReturnValues=ALL_OLD should return old attributes');
      const oldName = resp2.Attributes['name'];
      if (!oldName || !oldName.S || oldName.S !== 'Alice') throw new Error(`old name should be 'Alice', got ${oldName?.S}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: rvTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'PutItem_ReturnConsumedCapacity', async () => {
    const rcTable = `RCapP-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: rcTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new PutItemCommand({
        TableName: rcTable,
        Item: { id: { S: 'rc1' } },
        ReturnConsumedCapacity: 'TOTAL',
      }));
      if (!resp.ConsumedCapacity) throw new Error('expected ConsumedCapacity in PutItem response');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: rcTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'GetItem_ProjectionExpression', async () => {
    const resp = await client.send(new GetItemCommand({
      TableName: ctx.compTableName,
      Key: { pk: { S: 'user1' }, sk: { S: 'meta' } },
      ProjectionExpression: 'name, age',
    }));
    if (Object.keys(resp.Item ?? {}).length !== 2) throw new Error(`expected 2 projected attributes, got ${Object.keys(resp.Item ?? {}).length}`);
    if (!resp.Item?.['name']) throw new Error("expected 'name' in projection");
    if (!resp.Item?.['age']) throw new Error("expected 'age' in projection");
    if (resp.Item?.['pk']) throw new Error("did not expect 'pk' in projection");
  }));

  results.push(await runner.runTest('dynamodb', 'GetItem_ProjectionWithAttrNames', async () => {
    const resp = await client.send(new GetItemCommand({
      TableName: ctx.compTableName,
      Key: { pk: { S: 'user1' }, sk: { S: 'meta' } },
      ProjectionExpression: '#n, #a',
      ExpressionAttributeNames: { '#n': 'name', '#a': 'age' },
    }));
    if (Object.keys(resp.Item ?? {}).length !== 2) throw new Error(`expected 2 projected attributes, got ${Object.keys(resp.Item ?? {}).length}`);
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteItem_NonExistentKey_NoCondition', async () => {
    const delTable = `DelNE-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: delTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new DeleteItemCommand({
        TableName: delTable,
        Key: { id: { S: 'nonexistent' } },
      }));
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: delTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteItem_ReturnValuesAllOld', async () => {
    const rvDelTable = `RVDel-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: rvDelTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: rvDelTable,
        Item: { id: { S: 'rvdel1' }, name: { S: 'ToDelete' }, count: { N: '99' } },
      }));
      const resp = await client.send(new DeleteItemCommand({
        TableName: rvDelTable,
        Key: { id: { S: 'rvdel1' } },
        ReturnValues: 'ALL_OLD',
      }));
      if (!resp.Attributes) throw new Error('expected old attributes in response');
      const oldName = resp.Attributes['name'];
      if (!oldName || !oldName.S || oldName.S !== 'ToDelete') throw new Error(`expected old name 'ToDelete', got ${oldName?.S}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: rvDelTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'DeleteItem_ReturnValuesAllOld_NonExistent', async () => {
    const rvDelTable = `RVDelNE-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: rvDelTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new DeleteItemCommand({
        TableName: rvDelTable,
        Key: { id: { S: 'nonexistent' } },
        ReturnValues: 'ALL_OLD',
      }));
      if (resp.Attributes) throw new Error('expected nil Attributes for non-existent key');
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: rvDelTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_ReturnUpdatedAttributes', async () => {
    const uaTable = `UATable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: uaTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: uaTable,
        Item: {
          id: { S: 'ua1' },
          val: { N: '0' },
          tags: { L: [{ S: 'a' }] },
        },
      }));
      const resp = await client.send(new UpdateItemCommand({
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
      const val = resp.Attributes['val'];
      if (!val || !val.N || val.N !== '5') throw new Error(`expected val=5, got ${val?.N}`);
      const tags = resp.Attributes['tags'];
      if (!tags || !tags.L || tags.L.length !== 2) throw new Error(`expected 2 tags, got ${tags?.L?.length}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: uaTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_CreateNonExistent', async () => {
    const uaTable = `UACreate-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: uaTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new UpdateItemCommand({
        TableName: uaTable,
        Key: { id: { S: 'new1' } },
        UpdateExpression: 'SET #n = :name',
        ExpressionAttributeNames: { '#n': 'name' },
        ExpressionAttributeValues: { ':name': { S: 'CreatedViaUpdate' } },
        ReturnValues: 'ALL_NEW',
      }));
      if (!resp.Attributes) throw new Error('expected attributes');
      const idVal = resp.Attributes['id'];
      if (!idVal || !idVal.S || idVal.S !== 'new1') throw new Error(`expected id=new1, got ${idVal?.S}`);
      const nameVal = resp.Attributes['name'];
      if (!nameVal || !nameVal.S || nameVal.S !== 'CreatedViaUpdate') throw new Error(`expected name=CreatedViaUpdate, got ${nameVal?.S}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: uaTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_IfNotExists', async () => {
    const ineTable = `INETable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ineTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: ineTable,
        Item: { id: { S: 'ine1' }, val: { N: '10' } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: ineTable,
        Key: { id: { S: 'ine1' } },
        UpdateExpression: 'SET #v = if_not_exists(#v, :zero) + :inc',
        ExpressionAttributeNames: { '#v': 'val' },
        ExpressionAttributeValues: { ':zero': { N: '0' }, ':inc': { N: '5' } },
        ReturnValues: 'ALL_NEW',
      }));
      if (!resp.Attributes) throw new Error('expected attributes');
      const val = resp.Attributes['val'];
      if (!val || !val.N || val.N !== '15') throw new Error(`expected val=15 (10+5), got ${val?.N}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ineTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_IfNotExists_NoExisting', async () => {
    const ineTable = `INENE-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ineTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      const resp = await client.send(new UpdateItemCommand({
        TableName: ineTable,
        Key: { id: { S: 'inene1' } },
        UpdateExpression: 'SET #v = if_not_exists(#v, :zero) + :inc',
        ExpressionAttributeNames: { '#v': 'val' },
        ExpressionAttributeValues: { ':zero': { N: '0' }, ':inc': { N: '5' } },
        ReturnValues: 'ALL_NEW',
      }));
      const val = resp.Attributes?.['val'];
      if (!val || !val.N || val.N !== '5') throw new Error(`expected val=5 (0+5), got ${val?.N}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ineTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_Arithmetic', async () => {
    const arithTable = `Arith-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: arithTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: arithTable,
        Item: { id: { S: 'a1' }, val: { N: '100' } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: arithTable,
        Key: { id: { S: 'a1' } },
        UpdateExpression: 'SET #v = #v - :dec',
        ExpressionAttributeNames: { '#v': 'val' },
        ExpressionAttributeValues: { ':dec': { N: '30' } },
        ReturnValues: 'ALL_NEW',
      }));
      const val = resp.Attributes?.['val'];
      if (!val || !val.N || val.N !== '70') throw new Error(`expected val=70 (100-30), got ${val?.N}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: arithTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_Remove', async () => {
    const rmTable = `RmTable-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: rmTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: rmTable,
        Item: { id: { S: 'rm1' }, name: { S: 'Alice' }, email: { S: 'alice@test.com' } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: rmTable,
        Key: { id: { S: 'rm1' } },
        UpdateExpression: 'REMOVE email',
        ReturnValues: 'ALL_NEW',
      }));
      if (resp.Attributes?.['email']) throw new Error("expected 'email' to be removed");
      if (!resp.Attributes?.['name']) throw new Error("expected 'name' to remain");
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: rmTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_AddNumber', async () => {
    const addTable = `AddN-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: addTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: addTable,
        Item: { id: { S: 'an1' }, val: { N: '10' } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: addTable,
        Key: { id: { S: 'an1' } },
        UpdateExpression: 'ADD #v :inc',
        ExpressionAttributeNames: { '#v': 'val' },
        ExpressionAttributeValues: { ':inc': { N: '5' } },
        ReturnValues: 'ALL_NEW',
      }));
      const val = resp.Attributes?.['val'];
      if (!val || !val.N || val.N !== '15') throw new Error(`expected val=15, got ${val?.N}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: addTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_AddStringSet', async () => {
    const ssTable = `AddSS-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: ssTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: ssTable,
        Item: { id: { S: 'ss1' }, tags: { SS: ['a', 'b'] } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: ssTable,
        Key: { id: { S: 'ss1' } },
        UpdateExpression: 'ADD #t :newTags',
        ExpressionAttributeNames: { '#t': 'tags' },
        ExpressionAttributeValues: { ':newTags': { SS: ['b', 'c'] } },
        ReturnValues: 'ALL_NEW',
      }));
      const tags = resp.Attributes?.['tags'];
      if (!tags || !tags.SS) throw new Error('expected SS type for tags');
      if (tags.SS.length !== 3) throw new Error(`expected 3 tags (a,b,c), got ${tags.SS.length}`);
      const tagSet = new Set(tags.SS);
      for (const exp of ['a', 'b', 'c']) {
        if (!tagSet.has(exp)) throw new Error(`expected tag "${exp}" in set`);
      }
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: ssTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_DeleteStringSet', async () => {
    const dsTable = `DelSS-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: dsTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: dsTable,
        Item: { id: { S: 'ds1' }, tags: { SS: ['a', 'b', 'c'] } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: dsTable,
        Key: { id: { S: 'ds1' } },
        UpdateExpression: 'DELETE #t :remove',
        ExpressionAttributeNames: { '#t': 'tags' },
        ExpressionAttributeValues: { ':remove': { SS: ['a', 'c'] } },
        ReturnValues: 'ALL_NEW',
      }));
      const tags = resp.Attributes?.['tags'];
      if (!tags || !tags.SS) throw new Error('expected SS type for tags');
      if (tags.SS.length !== 1 || tags.SS[0] !== 'b') throw new Error(`expected tags=[b], got ${JSON.stringify(tags.SS)}`);
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: dsTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_UpdatedOld', async () => {
    const uoTable = `UO-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: uoTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: uoTable,
        Item: { id: { S: 'uo1' }, val: { N: '10' }, name: { S: 'Old' } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: uoTable,
        Key: { id: { S: 'uo1' } },
        UpdateExpression: 'SET #v = :new',
        ExpressionAttributeNames: { '#v': 'val' },
        ExpressionAttributeValues: { ':new': { N: '20' } },
        ReturnValues: 'UPDATED_OLD',
      }));
      if (!resp.Attributes) throw new Error('expected updated old attributes');
      if (!resp.Attributes['val']) throw new Error("expected 'val' in UPDATED_OLD response");
      if (resp.Attributes['name']) throw new Error("did not expect unchanged 'name' in UPDATED_OLD response");
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: uoTable }));
      });
    }
  }));

  results.push(await runner.runTest('dynamodb', 'UpdateItem_UpdatedNew', async () => {
    const unTable = `UN-${ts}`;
    await client.send(new CreateTableCommand({
      TableName: unTable,
      AttributeDefinitions: [{ AttributeName: 'id', AttributeType: 'S' }],
      KeySchema: [{ AttributeName: 'id', KeyType: 'HASH' }],
      BillingMode: 'PAY_PER_REQUEST',
    }));
    try {
      await client.send(new PutItemCommand({
        TableName: unTable,
        Item: { id: { S: 'un1' }, val: { N: '10' }, name: { S: 'Old' } },
      }));
      const resp = await client.send(new UpdateItemCommand({
        TableName: unTable,
        Key: { id: { S: 'un1' } },
        UpdateExpression: 'SET #v = :new',
        ExpressionAttributeNames: { '#v': 'val' },
        ExpressionAttributeValues: { ':new': { N: '20' } },
        ReturnValues: 'UPDATED_NEW',
      }));
      if (!resp.Attributes) throw new Error('expected updated new attributes');
      const val = resp.Attributes['val'];
      if (!val || !val.N || val.N !== '20') throw new Error(`expected val=20 in UPDATED_NEW, got ${val?.N}`);
      if (resp.Attributes['name']) throw new Error("did not expect unchanged 'name' in UPDATED_NEW response");
    } finally {
      await safeCleanup(async () => {
        await client.send(new DeleteTableCommand({ TableName: unTable }));
      });
    }
  }));

  return results;
}
