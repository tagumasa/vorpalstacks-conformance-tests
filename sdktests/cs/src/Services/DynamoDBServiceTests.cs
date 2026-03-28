using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class DynamoDBServiceTests
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
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tableName,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DescribeTable", async () =>
            {
                var resp = await dynamoClient.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                });
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
                        { "name", new AttributeValue { S = "Test Item" } }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "GetItem", async () =>
            {
                var resp = await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } }
                    }
                });
                if (resp.Item == null || resp.Item.Count == 0)
                    throw new Exception("Item is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "GetItem_NonExistentKey", async () =>
            {
                var resp = await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "nonexistent-key-xyz" } }
                    }
                });
                if (resp.Item != null && resp.Item.Count > 0)
                    throw new Exception("Expected empty item for non-existent key");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem", async () =>
            {
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } }
                    },
                    UpdateExpression = "SET #n = :name",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#n", "name" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":name", new AttributeValue { S = "Updated" } }
                    },
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
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":id", new AttributeValue { S = "test1" } }
                    }
                });
                if (resp.Count <= 0)
                    throw new Exception("Query returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "Scan", async () =>
            {
                var resp = await dynamoClient.ScanAsync(new ScanRequest
                {
                    TableName = tableName
                });
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
                                new WriteRequest
                                {
                                    PutRequest = new PutRequest
                                    {
                                        Item = new Dictionary<string, AttributeValue>
                                        {
                                            { "id", new AttributeValue { S = "batch1" } },
                                            { "data", new AttributeValue { S = "batch item 1" } }
                                        }
                                    }
                                },
                                new WriteRequest
                                {
                                    PutRequest = new PutRequest
                                    {
                                        Item = new Dictionary<string, AttributeValue>
                                        {
                                            { "id", new AttributeValue { S = "batch2" } },
                                            { "data", new AttributeValue { S = "batch item 2" } }
                                        }
                                    }
                                }
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
                                    new Dictionary<string, AttributeValue>
                                    {
                                        { "id", new AttributeValue { S = "batch1" } }
                                    },
                                    new Dictionary<string, AttributeValue>
                                    {
                                        { "id", new AttributeValue { S = "batch2" } }
                                    }
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
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "Environment", Value = "Test" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ListTagsOfResource", async () =>
            {
                var resp = await dynamoClient.ListTagsOfResourceAsync(new ListTagsOfResourceRequest
                {
                    ResourceArn = tableArn
                });
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
                await dynamoClient.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = tableName,
                    TimeToLiveSpecification = new TimeToLiveSpecification
                    {
                        AttributeName = "ttl",
                        Enabled = true
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DescribeTimeToLive", async () =>
            {
                var resp = await dynamoClient.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest
                {
                    TableName = tableName
                });
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
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ListBackups", async () =>
            {
                var resp = await dynamoClient.ListBackupsAsync(new ListBackupsRequest());
                if (resp.BackupSummaries == null)
                    throw new Exception("BackupSummaries is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DescribeContinuousBackups", async () =>
            {
                var resp = await dynamoClient.DescribeContinuousBackupsAsync(new DescribeContinuousBackupsRequest
                {
                    TableName = tableName
                });
                if (resp.ContinuousBackupsDescription == null)
                    throw new Exception("ContinuousBackupsDescription is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateContinuousBackups", async () =>
            {
                await dynamoClient.UpdateContinuousBackupsAsync(new UpdateContinuousBackupsRequest
                {
                    TableName = tableName,
                    PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification
                    {
                        PointInTimeRecoveryEnabled = true
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ExecuteStatement", async () =>
            {
                await dynamoClient.ExecuteStatementAsync(new ExecuteStatementRequest
                {
                    Statement = $"INSERT INTO \"{tableName}\" VALUE {{'id': 'partiq1', 'name': 'PartiQL Item'}}"
                });
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
                        new TransactWriteItem
                        {
                            Put = new Put
                            {
                                TableName = tableName,
                                Item = new Dictionary<string, AttributeValue>
                                {
                                    { "id", new AttributeValue { S = "txn1" } },
                                    { "name", new AttributeValue { S = "Transaction Item" } }
                                }
                            }
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "TransactGetItems", async () =>
            {
                var resp = await dynamoClient.TransactGetItemsAsync(new TransactGetItemsRequest
                {
                    TransactItems = new List<TransactGetItem>
                    {
                        new TransactGetItem
                        {
                            Get = new Get
                            {
                                TableName = tableName,
                                Key = new Dictionary<string, AttributeValue>
                                {
                                    { "id", new AttributeValue { S = "txn1" } }
                                }
                            }
                        }
                    }
                });
                if (resp.Responses == null || resp.Responses.Count <= 0)
                    throw new Exception("TransactGetItems returned no items");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "BatchExecuteStatement", async () =>
            {
                await dynamoClient.BatchExecuteStatementAsync(new BatchExecuteStatementRequest
                {
                    Statements = new List<BatchStatementRequest>
                    {
                        new BatchStatementRequest
                        {
                            Statement = $"UPDATE \"{tableName}\" SET \"name\" = 'batch updated' WHERE id = 'batch1'"
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "ExecuteTransaction", async () =>
            {
                var resp = await dynamoClient.ExecuteTransactionAsync(new ExecuteTransactionRequest
                {
                    TransactStatements = new List<ParameterizedStatement>
                    {
                        new ParameterizedStatement
                        {
                            Statement = $"SELECT * FROM \"{tableName}\" WHERE id = 'test1'"
                        }
                    }
                });
                if (resp.Responses == null || resp.Responses.Count <= 0)
                    throw new Exception("ExecuteTransaction returned no responses");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "UpdateTable", async () =>
            {
                var resp = await dynamoClient.UpdateTableAsync(new UpdateTableRequest
                {
                    TableName = tableName
                });
                if (resp.TableDescription == null)
                    throw new Exception("TableDescription is null");
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem", async () =>
            {
                await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("dynamodb", "DeleteTable", async () =>
            {
                await dynamoClient.DeleteTableAsync(new DeleteTableRequest
                {
                    TableName = tableName
                });
            }));
        }
        finally
        {
            try
            {
                await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tableName });
            }
            catch { }
        }

        // Error and edge case tests
        results.Add(await runner.RunTestAsync("dynamodb", "GetItem_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.GetItemAsync(new GetItemRequest
                {
                    TableName = "NonExistentTable_xyz",
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } }
                    }
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = "NonExistentTable_xyz",
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "name", new AttributeValue { S = "Test" } }
                    }
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.QueryAsync(new QueryRequest
                {
                    TableName = "NonExistentTable_xyz",
                    KeyConditionExpression = "id = :id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":id", new AttributeValue { S = "test1" } }
                    }
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Scan_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.ScanAsync(new ScanRequest
                {
                    TableName = "NonExistentTable_xyz"
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DescribeTable_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = "NonExistentTable_xyz"
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteTable_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.DeleteTableAsync(new DeleteTableRequest
                {
                    TableName = "NonExistentTable_xyz"
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_ConditionalCheckFail", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCondFail");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });

                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "status", new AttributeValue { S = "active" } }
                    }
                });

                try
                {
                    await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "id", new AttributeValue { S = "test1" } }
                        },
                        UpdateExpression = "SET #s = :status",
                        ConditionExpression = "#s = :expected",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            { "#s", "status" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":status", new AttributeValue { S = "inactive" } },
                            { ":expected", new AttributeValue { S = "deleted" } }
                        }
                    });
                    throw new Exception("Expected ConditionalCheckFailedException");
                }
                catch (ConditionalCheckFailedException)
                {
                }
            }
            finally
            {
                try { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem_ConditionalCheckFail", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSDelCondFail");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });

                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "status", new AttributeValue { S = "active" } }
                    }
                });

                try
                {
                    await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                    {
                        TableName = tempTable,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "id", new AttributeValue { S = "test1" } }
                        },
                        ConditionExpression = "#s = :expected",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            { "#s", "status" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":expected", new AttributeValue { S = "deleted" } }
                        }
                    });
                    throw new Exception("Expected ConditionalCheckFailedException");
                }
                catch (ConditionalCheckFailedException)
                {
                }
            }
            finally
            {
                try { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "BatchGetItem_NonExistentTable", async () =>
        {
            try
            {
                await dynamoClient.BatchGetItemAsync(new BatchGetItemRequest
                {
                    RequestItems = new Dictionary<string, KeysAndAttributes>
                    {
                        {
                            "NonExistentTable_xyz", new KeysAndAttributes
                            {
                                Keys = new List<Dictionary<string, AttributeValue>>
                                {
                                    new Dictionary<string, AttributeValue>
                                    {
                                        { "id", new AttributeValue { S = "test1" } }
                                    }
                                }
                            }
                        }
                    }
                });
                throw new Exception("Expected ResourceNotFoundException");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "CreateTable_DuplicateName", async () =>
        {
            var dupTable = TestRunner.MakeUniqueName("CSDup");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = dupTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });

                try
                {
                    await dynamoClient.CreateTableAsync(new CreateTableRequest
                    {
                        TableName = dupTable,
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                        },
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                        },
                        BillingMode = "PAY_PER_REQUEST"
                    });
                    throw new Exception("Expected ResourceInUseException");
                }
                catch (ResourceInUseException)
                {
                }
            }
            finally
            {
                try { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = dupTable }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "Query_ReturnConsumedCapacity", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSQCap");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });

                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "name", new AttributeValue { S = "Test Item" } }
                    }
                });

                var resp = await dynamoClient.QueryAsync(new QueryRequest
                {
                    TableName = tempTable,
                    KeyConditionExpression = "id = :id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":id", new AttributeValue { S = "test1" } }
                    },
                    ReturnConsumedCapacity = "TOTAL"
                });
                if (resp.ConsumedCapacity == null)
                    throw new Exception("ConsumedCapacity is null");
            }
            finally
            {
                try { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_ReturnValues", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSPutRV");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });

                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "name", new AttributeValue { S = "Before" } }
                    }
                });

                var resp = await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "name", new AttributeValue { S = "After" } }
                    },
                    ReturnValues = "ALL_OLD"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("name") || resp.Attributes["name"].S != "Before")
                    throw new Exception("PutItem ALL_OLD did not return old attributes");
            }
            finally
            {
                try { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); } catch { }
            }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_ReturnUpdatedAttributes", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSUpdRV");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = "id", AttributeType = "S" }
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "id", KeyType = "HASH" }
                    },
                    BillingMode = "PAY_PER_REQUEST"
                });

                await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } },
                        { "name", new AttributeValue { S = "Before" } }
                    }
                });

                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "test1" } }
                    },
                    UpdateExpression = "SET #n = :name",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#n", "name" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":name", new AttributeValue { S = "After" } }
                    },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("name") || resp.Attributes["name"].S != "After")
                    throw new Exception("UpdateItem ALL_NEW did not return updated attributes");
            }
            finally
            {
                try { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); } catch { }
            }
        }));

        return results;
    }
}
