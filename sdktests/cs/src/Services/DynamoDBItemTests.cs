using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VorpalStacks.SDK.Tests.Services;

public static partial class DynamoDBServiceTests
{
    public static async Task RunItemTests(TestRunner runner, AmazonDynamoDBClient dynamoClient, string compTableName, List<TestResult> results)
    {
        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_ConditionPass", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCondPut");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "cp1" } }, { "val", new AttributeValue { N = "10" } } },
                    ConditionExpression = "attribute_not_exists(id)"
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_ConditionFail", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSCondPutF");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "cpf1" } }, { "val", new AttributeValue { N = "10" } } }
                });
                await TestHelpers.AssertThrowsAsync<ConditionalCheckFailedException>(async () =>
                {
                    await dynamoClient.PutItemAsync(new PutItemRequest
                    {
                        TableName = tempTable,
                        Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "cpf1" } }, { "val", new AttributeValue { N = "20" } } },
                        ConditionExpression = "attribute_not_exists(id)"
                    });
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "PutItem_ReturnConsumedCapacity", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSRCapP");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.PutItemAsync(new PutItemRequest
                {
                    TableName = tempTable,
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rc1" } } },
                    ReturnConsumedCapacity = "TOTAL"
                });
                if (resp.ConsumedCapacity == null)
                    throw new Exception("expected ConsumedCapacity in PutItem response");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "GetItem_ProjectionExpression", async () =>
        {
            var resp = await dynamoClient.GetItemAsync(new GetItemRequest
            {
                TableName = compTableName,
                Key = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "user1" } }, { "sk", new AttributeValue { S = "meta" } } },
                ProjectionExpression = "name, age"
            });
            if (resp.Item == null || resp.Item.Count != 2)
                throw new Exception($"expected 2 projected attributes, got {resp.Item?.Count}");
            if (!resp.Item.ContainsKey("name"))
                throw new Exception("expected 'name' in projection");
            if (!resp.Item.ContainsKey("age"))
                throw new Exception("expected 'age' in projection");
            if (resp.Item.ContainsKey("pk"))
                throw new Exception("did not expect 'pk' in projection");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "GetItem_ProjectionWithAttrNames", async () =>
        {
            var resp = await dynamoClient.GetItemAsync(new GetItemRequest
            {
                TableName = compTableName,
                Key = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "user1" } }, { "sk", new AttributeValue { S = "meta" } } },
                ProjectionExpression = "#n, #a",
                ExpressionAttributeNames = new Dictionary<string, string> { { "#n", "name" }, { "#a", "age" } }
            });
            if (resp.Item == null || resp.Item.Count != 2)
                throw new Exception($"expected 2 projected attributes, got {resp.Item?.Count}");
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem_NonExistentKey_NoCondition", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSDelNE");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nonexistent" } } }
                });
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem_ReturnValuesAllOld", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSRVDel");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rvdel1" } }, { "name", new AttributeValue { S = "ToDelete" } }, { "count", new AttributeValue { N = "99" } } }
                });
                var resp = await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rvdel1" } } },
                    ReturnValues = "ALL_OLD"
                });
                if (resp.Attributes == null)
                    throw new Exception("expected old attributes in response");
                if (!resp.Attributes.ContainsKey("name") || resp.Attributes["name"].S != "ToDelete")
                    throw new Exception("expected old name 'ToDelete'");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "DeleteItem_ReturnValuesAllOld_NonExistent", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSRVDelNE");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "nonexistent" } } },
                    ReturnValues = "ALL_OLD"
                });
                if (resp.Attributes != null)
                    throw new Exception("expected nil Attributes for non-existent key");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_CreateNonExistent", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSUACreate");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "new1" } } },
                    UpdateExpression = "SET #n = :name",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#n", "name" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":name", new AttributeValue { S = "CreatedViaUpdate" } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null)
                    throw new Exception("expected attributes");
                if (!resp.Attributes.ContainsKey("id") || resp.Attributes["id"].S != "new1")
                    throw new Exception("expected id=new1");
                if (!resp.Attributes.ContainsKey("name") || resp.Attributes["name"].S != "CreatedViaUpdate")
                    throw new Exception("expected name=CreatedViaUpdate");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_IfNotExists", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSINETable");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ine1" } }, { "val", new AttributeValue { N = "10" } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ine1" } } },
                    UpdateExpression = "SET #v = if_not_exists(#v, :zero) + :inc",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":zero", new AttributeValue { N = "0" } }, { ":inc", new AttributeValue { N = "5" } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("val") || resp.Attributes["val"].N != "15")
                    throw new Exception($"expected val=15 (10+5), got {resp.Attributes?["val"]?.N}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_IfNotExists_NoExisting", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSINENE");
            try
            {
                await dynamoClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tempTable,
                    AttributeDefinitions = new List<AttributeDefinition> { new() { AttributeName = "id", AttributeType = "S" } },
                    KeySchema = new List<KeySchemaElement> { new() { AttributeName = "id", KeyType = "HASH" } },
                    BillingMode = "PAY_PER_REQUEST"
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "inene1" } } },
                    UpdateExpression = "SET #v = if_not_exists(#v, :zero) + :inc",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":zero", new AttributeValue { N = "0" } }, { ":inc", new AttributeValue { N = "5" } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("val") || resp.Attributes["val"].N != "5")
                    throw new Exception($"expected val=5 (0+5), got {resp.Attributes?["val"]?.N}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_Arithmetic", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSArith");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "a1" } }, { "val", new AttributeValue { N = "100" } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "a1" } } },
                    UpdateExpression = "SET #v = #v - :dec",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":dec", new AttributeValue { N = "30" } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("val") || resp.Attributes["val"].N != "70")
                    throw new Exception($"expected val=70 (100-30), got {resp.Attributes?["val"]?.N}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_Remove", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSRmTable");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rm1" } }, { "name", new AttributeValue { S = "Alice" } }, { "email", new AttributeValue { S = "alice@test.com" } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "rm1" } } },
                    UpdateExpression = "REMOVE email",
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null)
                    throw new Exception("expected attributes");
                if (resp.Attributes.ContainsKey("email"))
                    throw new Exception("expected 'email' to be removed");
                if (!resp.Attributes.ContainsKey("name"))
                    throw new Exception("expected 'name' to remain");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_AddNumber", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSAddN");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "an1" } }, { "val", new AttributeValue { N = "10" } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "an1" } } },
                    UpdateExpression = "ADD #v :inc",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":inc", new AttributeValue { N = "5" } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("val") || resp.Attributes["val"].N != "15")
                    throw new Exception($"expected val=15, got {resp.Attributes?["val"]?.N}");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_AddStringSet", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSAddSS");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ss1" } }, { "tags", new AttributeValue { SS = new List<string> { "a", "b" } } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ss1" } } },
                    UpdateExpression = "ADD #t :newTags",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#t", "tags" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":newTags", new AttributeValue { SS = new List<string> { "b", "c" } } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("tags"))
                    throw new Exception("expected tags");
                var tags = resp.Attributes["tags"].SS;
                if (tags == null || tags.Count != 3)
                    throw new Exception($"expected 3 tags (a,b,c), got {tags?.Count}");
                foreach (var exp in new[] { "a", "b", "c" })
                {
                    if (!tags.Contains(exp))
                        throw new Exception($"expected tag '{exp}' in set");
                }
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_DeleteStringSet", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSDelSS");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ds1" } }, { "tags", new AttributeValue { SS = new List<string> { "a", "b", "c" } } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "ds1" } } },
                    UpdateExpression = "DELETE #t :remove",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#t", "tags" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":remove", new AttributeValue { SS = new List<string> { "a", "c" } } } },
                    ReturnValues = "ALL_NEW"
                });
                if (resp.Attributes == null || !resp.Attributes.ContainsKey("tags"))
                    throw new Exception("expected tags");
                var tags = resp.Attributes["tags"].SS;
                if (tags == null || tags.Count != 1 || tags[0] != "b")
                    throw new Exception($"expected tags=[b], got [{string.Join(",", tags ?? new List<string>())}]");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_UpdatedOld", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSUO");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "uo1" } }, { "val", new AttributeValue { N = "10" } }, { "name", new AttributeValue { S = "Old" } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "uo1" } } },
                    UpdateExpression = "SET #v = :new",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":new", new AttributeValue { N = "20" } } },
                    ReturnValues = "UPDATED_OLD"
                });
                if (resp.Attributes == null)
                    throw new Exception("expected updated old attributes");
                if (!resp.Attributes.ContainsKey("val"))
                    throw new Exception("expected 'val' in UPDATED_OLD response");
                if (resp.Attributes.ContainsKey("name"))
                    throw new Exception("did not expect unchanged 'name' in UPDATED_OLD response");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));

        results.Add(await runner.RunTestAsync("dynamodb", "UpdateItem_UpdatedNew", async () =>
        {
            var tempTable = TestRunner.MakeUniqueName("CSUN");
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
                    Item = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "un1" } }, { "val", new AttributeValue { N = "10" } }, { "name", new AttributeValue { S = "Old" } } }
                });
                var resp = await dynamoClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = tempTable,
                    Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "un1" } } },
                    UpdateExpression = "SET #v = :new",
                    ExpressionAttributeNames = new Dictionary<string, string> { { "#v", "val" } },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":new", new AttributeValue { N = "20" } } },
                    ReturnValues = "UPDATED_NEW"
                });
                if (resp.Attributes == null)
                    throw new Exception("expected updated new attributes");
                if (!resp.Attributes.ContainsKey("val") || resp.Attributes["val"].N != "20")
                    throw new Exception($"expected val=20 in UPDATED_NEW, got {resp.Attributes?["val"]?.N}");
                if (resp.Attributes.ContainsKey("name"))
                    throw new Exception("did not expect unchanged 'name' in UPDATED_NEW response");
            }
            finally { await TestHelpers.SafeCleanupAsync(async () => { await dynamoClient.DeleteTableAsync(new DeleteTableRequest { TableName = tempTable }); }); }
        }));
    }
}
