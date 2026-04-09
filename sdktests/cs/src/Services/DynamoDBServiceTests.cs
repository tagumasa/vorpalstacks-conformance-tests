using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class DynamoDBServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonDynamoDBClient dynamoClient,
        string region)
    {
        var results = new List<TestResult>();
        var tableName = TestRunner.MakeUniqueName("CSTable");
        var tableArn = $"arn:aws:dynamodb:{region}:000000000000:table/{tableName}";

        try
        {
            results.Add(await runner.RunTestAsync("dynamodb", "CreateTable", async () =>
            {
                var resp = await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tableName,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new() { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new() { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DescribeTable", async () =>
            {
                var resp = await dynamoClient.DescribeTableAsync(new DescribeTableRequest { TableName = tableName });
                if (resp.Table == null)
                    throw new Exception("Table is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ListTables", async () =>
            {
                var resp = await dynamoClient.ListTablesAsync(new ListTablesRequest());
                if (resp.TableNames == null)
                    throw new Exception("TableNames is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "PutItem", async () =>
            {
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tableName,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "name", new AttributeValue { S = "Test Item" } },
                        { "count", new AttributeValue { N = "42" } }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "GetItem", async () =>
            {
                var resp = await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } } }
                });
                if (resp.Item == null || resp.Item.Count == 0)
                    throw new Exception("Item is null");
                if (!resp.Item.ContainsKey("name") || resp.Item["name"].S != "Test Item")
                    throw new Exception("name mismatch");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "GetItem_NonExistentKey", async () =>
            {
                var resp = await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nonexistent-key-xyz" } } }
                });
                if (resp.Item != null && resp.Item.Count > 0)
                    throw new Exception("Expected empty item for non-existent key");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem", async () =>
            {
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } } },
                    UpdateExpression = "SET #n = :name",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#n", "name" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":name", new AttributeValue { S = "Updated" } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("name") || resp.Attributes["name"].S != "Updated")
                    throw new Exception("UpdateItem did not return updated attributes");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "Query", async () =>
            {
                var resp = await dynamoClient.QueryAsync(new QueryRequest
                {
                    TableName = tableName,
                    KeyConditionExpression = "id = :id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":id", new AttributeValue { S = "test1" } } }
                });
                if (resp.Count <= 0)
                    throw new Exception("Query returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "Scan", async () =>
            {
                var resp = await dynamoClient.ScanAsync(new ScanRequest { TableName = tableName });
                if (resp.Count <= 0)
                    throw new Exception("Scan returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "BatchWriteItem", async () =>
            {
                var resp = await dynamoClient.BatchWriteItemAsync(new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        {
                            tableName, new List<WriteRequest>
                            {
                                new() { PutRequest = new PutRequest { Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "batch1" } }, { "data", new AttributeValue { S = "batch item 1" } } } } },
                                new() { PutRequest = new PutRequest { Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "batch2" } }, { "data", new AttributeValue { S = "batch item 2" } } } } }
                            }
                        }
                    }
                });
                if (resp.UnprocessedItems == null)
                    throw new Exception("UnprocessedItems is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "BatchGetItem", async () =>
            {
                var resp = await dynamoClient.BatchGetItemAsync(new BatchGetItemRequest
                {
                    RequestItems = new Dictionary<string, KeysAndAttributes>
                    {
                        {
                            tableName, new KeysAndAttributes
                            {
                                Keys = new List<Dictionary<string, AttributeValue>>
                                {
                                    new() { { "id", new AttributeValue { S = "batch1" } } },
                                    new() { { "id", new AttributeValue { S = "batch2" } } }
                                }
                            }
                        }
                    }
                });
                if (resp.Responses == null || !resp.Responses.ContainsKey(tableName) || resp.Responses[tableName].Count <= 0)
                    throw new Exception("BatchGetItem returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "TagResource", async () =>
            {
                await dynamoClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = tableArn,
                    Tags = new List<Tag> { new() { Key = "Environment", Value = "Test" } }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ListTagsOfResource", async () =>
            {
                var resp = await dynamoClient.ListTagsOfResourceAsync(new ListTagsOfResourceRequest { ResourceArn = tableArn });
                if (resp.Tags == null || resp.Tags.Count == 0)
                    throw new Exception("No tags found");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UntagResource", async () =>
            {
                await dynamoClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceArn = tableArn,
                    TagKeys = new List<string> { "Environment" }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateTimeToLive", async () =>
            {
                var resp = await dynamoClient.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = tableName,
                    TimeToLiveSpecification = new TimeToLiveSpecification { AttributeName = "ttl", Enabled = true }
                });
                if (resp.TimeToLiveSpecification == null)
                    throw new Exception("TimeToLiveSpecification is nil");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DescribeTimeToLive", async () =>
            {
                var resp = await dynamoClient.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest { TableName = tableName });
                if (resp.TimeToLiveDescription == null)
                    throw new Exception("TimeToLiveDescription is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "CreateBackup", async () =>
            {
                var resp = await dynamoClient.CreateBackupAsync(new CreateBackupRequest
                {
                    TableName = tableName,
                    BackupName = TestRunner.MakeUniqueName("CSBackup")
                });
                if (resp.BackupDetails == null)
                    throw new Exception("BackupDetails is null");
                await TestHelpers.SafeCleanupAsync(async () =>
                {
                    await dynamoClient.DeleteBackupAsync(new DeleteBackupRequest { BackupArn = resp.BackupDetails.BackupArn });
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ListBackups", async () =>
            {
                var resp = await dynamoClient.ListBackupsAsync(new ListBackupsRequest());
                if (resp.BackupSummaries == null)
                    throw new Exception("BackupSummaries is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DescribeContinuousBackups", async () =>
            {
                var resp = await dynamoClient.DescribeContinuousBackupsAsync(new DescribeContinuousBackupsRequest { TableName = tableName });
                if (resp.ContinuousBackupsDescription == null)
                    throw new Exception("ContinuousBackupsDescription is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateContinuousBackups", async () =>
            {
                var resp = await dynamoClient.UpdateContinuousBackupsAsync(new UpdateContinuousBackupsRequest
                {
                    TableName = tableName,
                    PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification { PointInTimeRecoveryEnabled = true }
                });
                if (resp.ContinuousBackupsDescription == null)
                    throw new Exception("ContinuousBackupsDescription is nil");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ExecuteStatement", async () =>
            {
                var resp = await dynamoClient.ExecuteStatementAsync(new ExecuteStatementRequest
                {
                    Statement = $"INSERT INTO \"{tableName}\" VALUE {{'id': 'partiq1', 'name': 'PartiQL Item'}}"
                });
                if (resp == null)
                    throw new Exception("ExecuteStatement response is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ExecuteStatement_Select", async () =>
            {
                var resp = await dynamoClient.ExecuteStatementAsync(new ExecuteStatementRequest
                {
                    Statement = $"SELECT * FROM \"{tableName}\" WHERE id = 'partiq1'"
                });
                if (resp.Items == null || resp.Items.Count <= 0)
                    throw new Exception("ExecuteStatement Select returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "TransactWriteItems", async () =>
            {
                await dynamoClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = new List<TransactWriteItem>
                    {
                        new() { Put = new Put { TableName = tableName, Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "txn1" } }, { "name", new AttributeValue { S = "Transaction Item" } } } } }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "TransactGetItems", async () =>
            {
                var resp = await dynamoClient.TransactGetItemsAsync(new TransactGetItemsRequest
                {
                    TransactItems = new List<TransactGetItem>
                    {
                        new() { Get = new Get { TableName = tableName, Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "txn1" } } } } }
                    }
                });
                if (resp.Responses == null || resp.Responses.Count <= 0)
                    throw new Exception("TransactGetItems returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "BatchExecuteStatement", async () =>
            {
                var resp = await dynamoClient.BatchExecuteStatementAsync(new BatchExecuteStatementRequest
                {
                    Statements = new List<BatchStatementRequest>
                    {
                        new() { Statement = $"UPDATE \"{tableName}\" SET \"name\" = 'batch updated' WHERE id = 'batch1'" }
                    }
                });
                if (resp.Responses == null)
                    throw new Exception("BatchExecuteStatement Responses is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ExecuteTransaction", async () =>
            {
                var resp = await dynamoClient.ExecuteTransactionAsync(new ExecuteTransactionRequest
                {
                    TransactStatements = new List<ParameterizedStatement>
                    {
                        new() { Statement = $"SELECT * FROM \"{tableName}\" WHERE id = 'test1'" }
                    }
                });
                if (resp.Responses == null || resp.Responses.Count <= 0)
                    throw new Exception("ExecuteTransaction returned no responses");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateTable", async () =>
            {
                var resp = await dynamoClient.UpdateTableAsync(new UpdateTableRequest { TableName = tableName });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem", async () =>
            {
                await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "test1" } } }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DeleteTable", async () =>
            {
                await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tableName });
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tableName });
            });
        }

        // Setup composite key table for tests that need it
        var compTableName = TestRunner.MakeUniqueName("CSCompTable");
        results.Add(await runner.RunTestAsync("dynamodb", "CreateTable_CompositeKey", async () =>
        {
            var resp = await dynamoClient.CreateTableAsync(new CreateTableRequest
            {
                TableName = compTableName,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new() { AttributeName = "pk", AttributeType = "S" },
                    new() { AttributeName = "sk", AttributeType = "S" }
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new() { AttributeName = "pk", KeyType = "HASH" },
                    new() { AttributeName = "sk", KeyType = "RANGE" }
                },
                BillingMode = "PAY_PER_REQUEST"
            });
            if (resp.TableDescription == null)
                throw new Exception("TableDescription is nil");
            if (resp.TableDescription.KeySchema == null || resp.TableDescription.KeySchema.Count != 2)
                throw new Exception("expected 2 key schema elements");
        }));

        var compItems = new List<Dictionary<string, AttributeValue>>
        {
            new() { { "pk", new AttributeValue { S = "user1" } }, { "sk", new AttributeValue { S = "meta" } }, { "name", new AttributeValue { S = "Alice" } }, { "age", new AttributeValue { N = "30" } }, { "active", new AttributeValue { BOOL = true } } },
            new() { { "pk", new AttributeValue { S = "user1" } }, { "sk", new AttributeValue { S = "order1" } }, { "amount", new AttributeValue { N = "100" } }, { "status", new AttributeValue { S = "shipped" } } },
            new() { { "pk", new AttributeValue { S = "user1" } }, { "sk", new AttributeValue { S = "order2" } }, { "amount", new AttributeValue { N = "200" } }, { "status", new AttributeValue { S = "pending" } } },
            new() { { "pk", new AttributeValue { S = "user1" } }, { "sk", new AttributeValue { S = "order3" } }, { "amount", new AttributeValue { N = "50" } }, { "status", new AttributeValue { S = "delivered" } } },
            new() { { "pk", new AttributeValue { S = "user2" } }, { "sk", new AttributeValue { S = "meta" } }, { "name", new AttributeValue { S = "Bob" } }, { "age", new AttributeValue { N = "25" } }, { "active", new AttributeValue { BOOL = false } } },
            new() { { "pk", new AttributeValue { S = "user2" } }, { "sk", new AttributeValue { S = "order1" } }, { "amount", new AttributeValue { N = "300" } }, { "status", new AttributeValue { S = "shipped" } } }
        };
        foreach (var item in compItems)
        {
            await TestHelpers.SafeCleanupAsync(async () =>
            {
                await dynamoClient.PutItemAsync(new PutItemRequest { TableName = compTableName, Item = item });
            });
        }

        await RunErrorTests(runner, dynamoClient, results);
        await RunItemTests(runner, dynamoClient, compTableName, results);
        await RunConditionTests(runner, dynamoClient, results);
        await RunQueryTests(runner, dynamoClient, compTableName, results);

        // Clean up composite key table
        await TestHelpers.SafeCleanupAsync(async () =>
        {
            await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = compTableName });
        });

        return results;
    }
}
