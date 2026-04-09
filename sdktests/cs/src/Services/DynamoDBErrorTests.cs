using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class DynamoDBServiceTests
{
    public static async Task RunErrorTests(TestRunner runner, AmazonDynamoDBClient dynamoClient, List<TestResult> results)
    {
        results.Add(await runner.RunTestAsync("dynamodb", "GetItem_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = "NoSuchTable_xyz",
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "k" } } }
                });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = "NoSuchTable_xyz",
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "k" } } }
                });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.QueryAsync(new QueryRequest
                {
                    TableName = "NoSuchTable_xyz",
                    KeyConditionExpression = "id = :id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":id", new AttributeValue { S = "k" } } }
                });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Scan_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.ScanAsync(new ScanRequest { TableName = "NoSuchTable_xyz" });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DescribeTable_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.DescribeTableAsync(new DescribeTableRequest { TableName = "NoSuchTable_xyz" });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteTable_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = "NoSuchTable_xyz" });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_ConditionalCheckFail", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCondFail");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } }, { "status", new AttributeValue { S = "active" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } } },
                        UpdateExpression = "SET #s = :status",
                        ConditionExpression = "#s = :expected",
                        ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":status", new AttributeValue { S = "inactive" } }, { ":expected", new AttributeValue { S = "deleted" } } }
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "GetItem_NonExistentKey_EmptyResponse", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSGetErr");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nonexistent" } } }
                });
                if (resp.Item != null && resp.Item.Count != 0)
                    throw new Exception($"Expected empty item for non-existent key, got {resp.Item.Count} attributes");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem_ConditionalCheckFail", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSDelCondFail");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } }, { "status", new AttributeValue { S = "active" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } } },
                        ConditionExpression = "attribute_not_exists(id)"
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "BatchGetItem_NonExistentTable", async () =>
        {
            await TestHelpers.AssertThrowsAsync<ResourceNotFoundException>(async () =>
            {
                await dynamoClient.BatchGetItemAsync(new BatchGetItemRequest
                {
                    RequestItems = new Dictionary<string, KeysAndAttributes>
                    {
                        {
                            "NonExistentTable_xyz", new KeysAndAttributes
                            {
                                Keys = new List<Dictionary<string, AttributeValue>> { new() { { "id", new AttributeValue { S = "k" } } } }
                            }
                        }
                    }
                });
            });
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "CreateTable_DuplicateName", async () =>
        {
            var dupTable = TestRunner.MakeUniqueName("CSDup");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = dupTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await TestHelpers.AssertThrowsAsync<ResourceInUseException>(async () =>
                {
                    await dynamoClient.CreateTableAsync(new CreateTableRequest
                    {
                        TableName = dupTable,
                        AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                        KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                        BillingMode = "PAY_PER_REQUEST"
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = dupTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_ReturnConsumedCapacity", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSQCap");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "pk", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "pk", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "key1" } } }
                });
                var resp = await dynamoClient.QueryAsync(new QueryRequest
                {
                    TableName = tempTable,
                    KeyConditionExpression = "pk = :pk",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk", new AttributeValue { S = "key1" } } },
                    ReturnConsumedCapacity = "TOTAL"
                });
                if (resp.ConsumedCapacity == null)
                    throw new Exception("ConsumedCapacity is null");
                if (resp.ConsumedCapacity.TableName != tempTable)
                    throw new Exception($"ConsumedCapacity.TableName mismatch, got {resp.ConsumedCapacity.TableName}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_ReturnValues", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSPutRV");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp1 = await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rv1" } }, { "name", new AttributeValue { S = "Alice" } }, { "count", new AttributeValue { N = "10" } } },
                    ReturnValues = "ALL_OLD"
                });
                if (resp1.Attributes != null)
                    throw new Exception("first PutItem with ReturnValues=ALL_OLD should have nil Attributes for new item");

                var resp2 = await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rv1" } }, { "name", new AttributeValue { S = "Bob" } }, { "count", new AttributeValue { N = "20" } } },
                    ReturnValues = "ALL_OLD"
                });
                if (resp2.Attributes == null)
                    throw new Exception("second PutItem with ReturnValues=ALL_OLD should return old attributes");
                if (!resp2.Attributes.ContainsKey("name") || resp2.Attributes["name"].S != "Alice")
                    throw new Exception("old name should be 'Alice'");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_ReturnUpdatedAttributes", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSUpdRV");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ua1" } }, { "val", new AttributeValue { N = "0" } }, { "tags", new AttributeValue { L = new List<AttributeValue> { new() { S = "a" } } } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ua1" } } },
                    UpdateExpression = "ADD #v :inc SET #t = list_append(#t, :newTag)",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" }, { "#t", "tags" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":inc", new AttributeValue { N = "5" } }, { ":newTag", new AttributeValue { L = new List<AttributeValue> { new() { S = "b" } } } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null)
                    throw new Exception("expected updated attributes");
                if (!resp.Attributes.ContainsKey("val") || resp.Attributes["val"].N != "5")
                    throw new Exception($"expected val=5, got {resp.Attributes["val"]?.N}");
                if (!resp.Attributes.ContainsKey("tags") || resp.Attributes["tags"].L == null || resp.Attributes["tags"].L.Count != 2)
                    throw new Exception("expected 2 tags");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));
    }
}
