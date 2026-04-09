using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class DynamoDBServiceTests
{
    public static async Task RunQueryTests(TestRunner runner, AmazonDynamoDBClient dynamoClient, string compTableName, List<TestResult> results)
    {
        results.Add(await runner.RunTestAsync("dynamodb", "Query_CompositeKey", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } } }
            });
            if (resp.Count != 4)
                throw new Exception($"expected 4 items for user1, got {resp.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_SortKeyCondition", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk AND sk = :sk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } }, { ":sk", new AttributeValue { S = "order2" } } }
            });
            if (resp.Count != 1)
                throw new Exception($"expected 1 item, got {resp.Count}");
            if (!resp.Items[0].ContainsKey("amount") || resp.Items[0]["amount"].N != "200")
                throw new Exception($"expected amount=200, got {resp.Items[0]?["amount"]?.N}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_SortKeyBeginsWith", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } }, { ":prefix", new AttributeValue { S = "order" } } }
            });
            if (resp.Count != 3)
                throw new Exception($"expected 3 order items, got {resp.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_SortKeyBetween", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk AND sk BETWEEN :low AND :high",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } }, { ":low", new AttributeValue { S = "order1" } }, { ":high", new AttributeValue { S = "order3" } } }
            });
            if (resp.Count != 3)
                throw new Exception($"expected 3 items in BETWEEN, got {resp.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_ScanIndexForward", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } } },
                ScanIndexForward = false
            });
            if (resp.Count != 4)
                throw new Exception($"expected 4 items, got {resp.Count}");
            if (!resp.Items[0].ContainsKey("sk") || resp.Items[0]["sk"].S != "order3")
                throw new Exception($"expected first item sk=order3 in descending order, got {resp.Items[0]?["sk"]?.S}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_FilterExpression", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                FilterExpression = "#s = :status",
                ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } }, { ":prefix", new AttributeValue { S = "order" } }, { ":status", new AttributeValue { S = "shipped" } } }
            });
            if (resp.Count != 1)
                throw new Exception($"expected 1 shipped order, got {resp.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_Limit", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } } },
                Limit = 2
            });
            if (resp.Count > 2)
                throw new Exception($"expected at most 2 items with Limit=2, got {resp.Count}");
            if (resp.LastEvaluatedKey == null || resp.LastEvaluatedKey.Count == 0)
                throw new Exception("expected LastEvaluatedKey when Limit < total items");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_ProjectionExpression", async () =>
        {
            var resp = await dynamoClient.QueryAsync(new QueryRequest
            {
                TableName = compTableName,
                KeyConditionExpression = "pk = :pk AND sk = :sk",
                ProjectionExpression = "pk, sk, amount",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "user1" } }, { ":sk", new AttributeValue { S = "order1" } } }
            });
            if (resp.Count != 1)
                throw new Exception($"expected 1 item, got {resp.Count}");
            if (resp.Items[0].Count != 3)
                throw new Exception($"expected 3 projected attributes, got {resp.Items[0].Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Scan_FilterExpression", async () =>
        {
            var resp = await dynamoClient.ScanAsync(new ScanRequest
            {
                TableName = compTableName,
                FilterExpression = "#s = :status",
                ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":status", new AttributeValue { S = "shipped" } } }
            });
            if (resp.Count != 2)
                throw new Exception($"expected 2 shipped items, got {resp.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Scan_ProjectionExpression", async () =>
        {
            var resp = await dynamoClient.ScanAsync(new ScanRequest
            {
                TableName = compTableName,
                ProjectionExpression = "pk, name"
            });
            if (resp.Items == null)
                throw new Exception("Items is null");
            foreach (var item in resp.Items)
            {
                if (!item.ContainsKey("pk"))
                    throw new Exception("expected 'pk' in projected item");
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Scan_Limit", async () =>
        {
            var resp = await dynamoClient.ScanAsync(new ScanRequest { TableName = compTableName, Limit = 3 });
            if (resp.Count > 3)
                throw new Exception($"expected at most 3 items with Limit=3, got {resp.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "BatchWriteItem_DeleteRequest", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSBWDel");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "del1" } }, { "name", new AttributeValue { S = "ToDelete" } } }
                });
                await dynamoClient.BatchWriteItemAsync(new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        { tempTable, new List<WriteRequest> { new() { DeleteRequest = new DeleteRequest { Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "del1" } } } } } } }
                    }
                });
                var getResp = await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "del1" } } }
                });
                if (getResp.Item != null && getResp.Item.Count != 0)
                    throw new Exception("item should be deleted after BatchWriteItem DeleteRequest");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "BatchGetItem_Projection", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSBGProj");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "bp1" } }, { "name", new AttributeValue { S = "Alice" } }, { "email", new AttributeValue { S = "alice@test.com" } } }
                });
                var resp = await dynamoClient.BatchGetItemAsync(new BatchGetItemRequest
                {
                    RequestItems = new Dictionary<string, KeysAndAttributes>
                    {
                        { tempTable, new KeysAndAttributes { Keys = new List<Dictionary<string, AttributeValue>> { new() { { "id", new AttributeValue { S = "bp1" } } } }, ProjectionExpression = "id, name" } }
                    }
                });
                if (resp.Responses == null || !resp.Responses.ContainsKey(tempTable))
                    throw new Exception("no responses");
                var items = resp.Responses[tempTable];
                if (items.Count != 1 || items[0].Count != 2)
                    throw new Exception($"expected 1 item with 2 projected attributes, got {items.Count} items");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "TransactWriteItems_MultipleOps", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSTWMulti");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "t1" } }, { "val", new AttributeValue { N = "10" } } }
                });
                await dynamoClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = new List<TransactWriteItem>
                    {
                        new() { Update = new Update { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "t1" } } }, UpdateExpression = "SET #v = #v + :inc", ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } }, ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":inc", new AttributeValue { N = "5" } } } } },
                        new() { Put = new Put { TableName = tempTable, Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "t2" } }, { "val", new AttributeValue { N = "42" } } } } },
                        new() { ConditionCheck = new ConditionCheck { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "t1" } } }, ConditionExpression = "attribute_exists(id)" } }
                    }
                });
                var getResp1 = await dynamoClient.GetItemAsync(new GetItemRequest { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "t1" } } } });
                if (getResp1.Item == null || !getResp1.Item.ContainsKey("val") || getResp1.Item["val"].N != "15")
                    throw new Exception($"expected val=15 after transact update, got {getResp1.Item?["val"]?.N}");
                var getResp2 = await dynamoClient.GetItemAsync(new GetItemRequest { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "t2" } } } });
                if (getResp2.Item == null)
                    throw new Exception("expected t2 to be created");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "TransactWriteItems_ConditionFail", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSTWCondF");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nofail" } }, { "val", new AttributeValue { N = "1" } } }
                });
                await TestHelpers.AssertThrowsAsync<TransactionCanceledException>(async () =>
                {
                    await dynamoClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
                    {
                        TransactItems = new List<TransactWriteItem>
                        {
                            new() { Update = new Update { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nofail" } } }, UpdateExpression = "SET #v = :x", ConditionExpression = "#v = :expect", ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } }, ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":x", new AttributeValue { N = "2" } }, { ":expect", new AttributeValue { N = "999" } } } } }
                        }
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "TransactWriteItems_Delete", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSTWDel");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "td1" } }, { "name", new AttributeValue { S = "ToDelete" } } }
                });
                await dynamoClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = new List<TransactWriteItem>
                    {
                        new() { Delete = new Delete { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "td1" } } } } }
                    }
                });
                var getResp = await dynamoClient.GetItemAsync(new GetItemRequest { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "td1" } } } });
                if (getResp.Item != null && getResp.Item.Count != 0)
                    throw new Exception("item should be deleted after TransactWriteItems Delete");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "TransactGetItems_NonExistentKey", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSTGNE");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.TransactGetItemsAsync(new TransactGetItemsRequest
                {
                    TransactItems = new List<TransactGetItem>
                    {
                        new() { Get = new Get { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nonexistent" } } } } }
                    }
                });
                if (resp.Responses == null || resp.Responses.Count != 1)
                    throw new Exception($"expected 1 response, got {resp.Responses?.Count}");
                if (resp.Responses[0].Item != null && resp.Responses[0].Item.Count != 0)
                    throw new Exception("expected empty Item for non-existent key");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "ExecuteStatement_SelectWhere", async () =>
        {
            var resp = await dynamoClient.ExecuteStatementAsync(new ExecuteStatementRequest
            {
                Statement = $"SELECT * FROM \"{compTableName}\" WHERE pk = 'user1' AND sk = 'order2'"
            });
            if (resp.Items == null || resp.Items.Count != 1)
                throw new Exception($"expected 1 item, got {resp.Items?.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "ExecuteStatement_Update", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSPQUpd");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "pu1" } }, { "val", new AttributeValue { N = "10" } } }
                });
                await dynamoClient.ExecuteStatementAsync(new ExecuteStatementRequest
                {
                    Statement = $"UPDATE \"{tempTable}\" SET val = 20 WHERE id = 'pu1'"
                });
                var getResp = await dynamoClient.GetItemAsync(new GetItemRequest { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "pu1" } } } });
                if (getResp.Item == null || !getResp.Item.ContainsKey("val") || getResp.Item["val"].N != "20")
                    throw new Exception($"expected val=20 after PartiQL UPDATE, got {getResp.Item?["val"]?.N}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "ExecuteStatement_Delete", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSPQDel");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "pd1" } }, { "val", new AttributeValue { N = "99" } } }
                });
                await dynamoClient.ExecuteStatementAsync(new ExecuteStatementRequest
                {
                    Statement = $"DELETE FROM \"{tempTable}\" WHERE id = 'pd1'"
                });
                var getResp = await dynamoClient.GetItemAsync(new GetItemRequest { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "pd1" } } } });
                if (getResp.Item != null && getResp.Item.Count != 0)
                    throw new Exception("item should be deleted after PartiQL DELETE");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "ExecuteTransaction_MixedReadWrite", async () =>
        {
            try
            {
                await dynamoClient.ExecuteTransactionAsync(new ExecuteTransactionRequest
                {
                    TransactStatements = new List<ParameterizedStatement>
                    {
                        new() { Statement = $"SELECT * FROM \"{compTableName}\" WHERE pk = 'user1'" },
                        new() { Statement = $"INSERT INTO \"{compTableName}\" VALUE {{'pk': 'user3', 'sk': 'meta', 'name': 'Charlie'}}" }
                    }
                });
                throw new Exception("expected error for mixed read/write");
            }
            catch (Exception ex) when (ex is TransactionCanceledException || ex.Message.Contains("TransactionConflict"))
            { }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "ExecuteTransaction_WriteOnly", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSETWrite");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.ExecuteTransactionAsync(new ExecuteTransactionRequest
                {
                    TransactStatements = new List<ParameterizedStatement>
                    {
                        new() { Statement = $"INSERT INTO \"{tempTable}\" VALUE {{'id': 'et1', 'val': 'hello'}}" },
                        new() { Statement = $"INSERT INTO \"{tempTable}\" VALUE {{'id': 'et2', 'val': 'world'}}" }
                    }
                });
                var getResp = await dynamoClient.GetItemAsync(new GetItemRequest { TableName = tempTable, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "et1" } } } });
                if (getResp.Item == null)
                    throw new Exception("expected et1 to be created");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "CreateTable_Validation_NoKeySchema", async () =>
        {
            await TestHelpers.AssertThrowsAsync<AmazonDynamoDBException>(async () =>
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest { TableName = "BadTable" });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteTable_Protected", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSDP");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST",
                    DeletionProtectionEnabled = true
                });
                await TestHelpers.AssertThrowsAsync<ResourceInUseException>(async () =>
                {
                    await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable });
                });
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () =>
                {
                    await dynamoClient.UpdateTableAsync(new UpdateTableRequest { TableName = tempTable, DeletionProtectionEnabled = false });
                    await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable });
                });
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "CreateTable_GSI", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSGSITable");
            try
            {
                var resp = await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new() { AttributeName = "pk", AttributeType = "S" },
                        new() { AttributeName = "sk", AttributeType = "S" },
                        new() { AttributeName = "gsi_pk", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new() { AttributeName = "pk", KeyType = "HASH" },
                        new() { AttributeName = "sk", KeyType = "RANGE" }
                    },
                    BillingMode = "PAY_PER_REQUEST",
                    GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                    {
                        new()
                        {
                            IndexName = "gsi1",
                            KeySchema = new List<KeySchemaElement>
                            {
                                new() { AttributeName = "gsi_pk", KeyType = "HASH" },
                                new() { AttributeName = "sk", KeyType = "RANGE" }
                            },
                            Projection = new Projection { ProjectionType = "ALL" }
                        }
                    }
                });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is null");
                if (resp.TableDescription.GlobalSecondaryIndexes == null || resp.TableDescription.GlobalSecondaryIndexes.Count != 1)
                    throw new Exception("expected 1 GSI in description");
                if (resp.TableDescription.GlobalSecondaryIndexes[0].IndexName != "gsi1")
                    throw new Exception("expected GSI name 'gsi1'");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "CreateTable_StreamSpec", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSStreamTable");
            try
            {
                var resp = await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST",
                    StreamSpecification = new StreamSpecification { StreamEnabled = true, StreamViewType = "NEW_IMAGE" }
                });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is null");
                if (resp.TableDescription.StreamSpecification == null || resp.TableDescription.StreamSpecification.StreamEnabled != true)
                    throw new Exception("expected StreamEnabled=true");
                if (string.IsNullOrEmpty(resp.TableDescription.LatestStreamArn))
                    throw new Exception("expected LatestStreamArn");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateTable_EnableSSE", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSSSETable");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.UpdateTableAsync(new UpdateTableRequest
                {
                    TableName = tempTable,
                    SSESpecification = new SSESpecification { Enabled = true, SSEType = "AES256" }
                });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is null");
                if (resp.TableDescription.SSEDescription == null)
                    throw new Exception("expected SSEDescription");
                if (resp.TableDescription.SSEDescription.Status != "ENABLED")
                    throw new Exception($"expected SSEStatus=ENABLED, got {resp.TableDescription.SSEDescription.Status}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateTable_AddGSI", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSAddGSI");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" }, new() { AttributeName = "gsi_pk", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.UpdateTableAsync(new UpdateTableRequest
                {
                    TableName = tempTable,
                    GlobalSecondaryIndexUpdates = new List<GlobalSecondaryIndexUpdate>
                    {
                        new()
                        {
                            Create = new CreateGlobalSecondaryIndexAction
                            {
                                IndexName = "new_gsi",
                                KeySchema = new List<KeySchemaElement> { new() { AttributeName = "gsi_pk", KeyType = "HASH" } },
                                Projection = new Projection { ProjectionType = "ALL" }
                            }
                        }
                    }
                });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is nil");
                if (resp.TableDescription.GlobalSecondaryIndexes == null || resp.TableDescription.GlobalSecondaryIndexes.Count != 1)
                    throw new Exception("expected 1 GSI after update");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateTimeToLive_Disable", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSTTLDis");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = tempTable,
                    TimeToLiveSpecification = new TimeToLiveSpecification { AttributeName = "ttl", Enabled = true }
                });
                var resp = await dynamoClient.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = tempTable,
                    TimeToLiveSpecification = new TimeToLiveSpecification { AttributeName = "ttl", Enabled = false }
                });
                if (resp.TimeToLiveSpecification == null)
                    throw new Exception("TimeToLiveSpecification is nil");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_Limit_Pagination", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSPagTable");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "pk", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "pk", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                for (int i = 0; i < 5; i++)
                {
                    await dynamoClient.PutItemAsync(new PutItemRequest
                    {
                        TableName = tempTable,
                        Item = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = $"item-{i}" } }, { "data", new AttributeValue { S = "pagination" } } }
                    });
                }
                var allItems = new List<string>();
                Dictionary<string, AttributeValue>? exclusiveStartKey = null;
                int pageCount = 0;
                while (true)
                {
                    var req = new QueryRequest
                    {
                        TableName = tempTable,
                        KeyConditionExpression = "pk = :pk",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "item-0" } } },
                        Limit = 2
                    };
                    if (exclusiveStartKey != null)
                        req.ExclusiveStartKey = exclusiveStartKey;
                    var resp = await dynamoClient.QueryAsync(req);
                    pageCount++;
                    foreach (var item in resp.Items)
                    {
                        if (item.ContainsKey("pk"))
                            allItems.Add(item["pk"].S);
                    }
                    if (resp.LastEvaluatedKey != null && resp.LastEvaluatedKey.Count > 0)
                        exclusiveStartKey = resp.LastEvaluatedKey;
                    else
                        break;
                }
                if (allItems.Count != 1)
                    throw new Exception($"expected 1 item for pk=item-0, got {allItems.Count}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));
    }
}
