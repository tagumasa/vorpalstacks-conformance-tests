using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class DynamoDBServiceTests
{
    public static async Task RunConditionTests(TestRunner runner, AmazonDynamoDBClient dynamoClient, List<TestResult> results)
    {
        results.Add(await runner.RunTestAsync("dynamodb", "Condition_AttributeExists_True", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCE");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ce1" } }, { "name", new AttributeValue { S = "Test" } } }
                });
                await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ce1" } } },
                    UpdateExpression = "SET #s = :v",
                    ConditionExpression = "attribute_exists(name)",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "active" } } }
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Condition_AttributeNotExists_False", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCENE");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "cene1" } }, { "name", new AttributeValue { S = "Test" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "cene1" } } },
                        UpdateExpression = "SET #s = :v",
                        ConditionExpression = "attribute_not_exists(name)",
                        ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "active" } } }
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Condition_BeginsWith", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSBW");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "bw1" } }, { "name", new AttributeValue { S = "HelloWorld" } } }
                });
                await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "bw1" } } },
                    UpdateExpression = "SET #s = :v",
                    ConditionExpression = "begins_with(name, :prefix)",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "matched" } }, { ":prefix", new AttributeValue { S = "Hello" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "bw1" } } },
                        UpdateExpression = "SET #s = :v",
                        ConditionExpression = "begins_with(name, :prefix)",
                        ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "nope" } }, { ":prefix", new AttributeValue { S = "XYZ" } } }
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Condition_Contains", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCT");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ct1" } }, { "tags", new AttributeValue { SS = new List<string> { "go", "java", "python" } } } }
                });
                await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ct1" } } },
                    UpdateExpression = "SET #s = :v",
                    ConditionExpression = "contains(tags, :tag)",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "matched" } }, { ":tag", new AttributeValue { S = "java" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ct1" } } },
                        UpdateExpression = "SET #s = :v",
                        ConditionExpression = "contains(tags, :tag)",
                        ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "nope" } }, { ":tag", new AttributeValue { S = "rust" } } }
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Condition_ComparisonOperators", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCO");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "co1" } }, { "val", new AttributeValue { N = "10" } } }
                });
                var testCases = new[]
                {
                    ("#v = :x", "10", true), ("#v <> :x", "20", true),
                    ("#v < :x", "20", true), ("#v <= :x", "10", true),
                    ("#v > :x", "5", true), ("#v >= :x", "10", true),
                    ("#v < :x", "5", false), ("#v > :x", "20", false)
                };
                foreach (var (cond, val, pass) in testCases)
                {
                    try
                    {
                        await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                        {
                            TableName = tempTable,
                            Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "co1" } } },
                            UpdateExpression = "SET #s = :s",
                            ConditionExpression = cond,
                            ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" }, { "#s", "status" } },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":s", new AttributeValue { S = "ok" } }, { ":x", new AttributeValue { N = val } } }
                        });
                        if (!pass)
                            throw new Exception($"condition '{cond}' with val '{val}' should fail");
                    }
                    catch (ConditionalCheckFailedException)
                    {
                        if (pass)
                            throw new Exception($"condition '{cond}' with val '{val}' should pass");
                    }
                }
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Condition_AND_OR", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSAO");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ao1" } }, { "val", new AttributeValue { N = "10" } }, { "active", new AttributeValue { BOOL = true } } }
                });
                await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ao1" } } },
                    UpdateExpression = "SET #s = :v",
                    ConditionExpression = "active = :t AND #v > :x",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" }, { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "and-pass" } }, { ":t", new AttributeValue { BOOL = true } }, { ":x", new AttributeValue { N = "5" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ao1" } } },
                        UpdateExpression = "SET #s = :v",
                        ConditionExpression = "active = :f AND #v > :x",
                        ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" }, { "#v", "val" } },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "and-fail" } }, { ":f", new AttributeValue { BOOL = false } }, { ":x", new AttributeValue { N = "5" } } }
                    });
                });
                await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ao1" } } },
                    UpdateExpression = "SET #s = :v",
                    ConditionExpression = "active = :f OR #v > :x",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" }, { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v", new AttributeValue { S = "or-pass" } }, { ":f", new AttributeValue { BOOL = false } }, { ":x", new AttributeValue { N = "5" } } }
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));
    }
}
