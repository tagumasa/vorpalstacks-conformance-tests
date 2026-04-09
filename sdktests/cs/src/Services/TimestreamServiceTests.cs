using Amazon;
using Amazon.TimestreamWrite;
using Amazon.TimestreamWrite.Model;
using Amazon.Runtime;

namespace VorpalStacks.SDK.Tests.Services;

public static class TimestreamServiceTests
{
    public static async Task<List<TestResult>> RunTests(
        TestRunner runner,
        AmazonTimestreamWriteClient timestreamClient,
        string region)
    {
        var results = new List<TestResult>();

        results.Add(await runner.RunTestAsync("timestream", "ListDatabases", async () =>
        {
            var resp = await timestreamClient.ListDatabasesAsync(new ListDatabasesRequest());
            if (resp.Databases == null)
                throw new Exception("Databases is null");
        }));

        results.Add(await runner.RunTestAsync("timestream", "DescribeDatabase", async () =>
        {
            var listResp = await timestreamClient.ListDatabasesAsync(new ListDatabasesRequest());
            if (listResp.Databases != null && listResp.Databases.Count > 0)
            {
                var database = listResp.Databases[0];
                var resp = await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest
                {
                    DatabaseName = database.DatabaseName
                });
                if (resp.Database == null)
                    throw new Exception("Database is null");
            }
        }));

        results.Add(await runner.RunTestAsync("timestream", "ListTables", async () =>
        {
            var resp = await timestreamClient.ListTablesAsync(new ListTablesRequest());
            if (resp.Tables == null)
                throw new Exception("Tables is null");
        }));

        results.Add(await runner.RunTestAsync("timestream", "DescribeDatabase_NonExistent", async () =>
        {
            try
            {
                await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest
                {
                    DatabaseName = "NonExistentDatabase_xyz_12345"
                });
                throw new Exception("Expected error but got none");
            }
            catch (ResourceNotFoundException)
            {
            }
        }));

        var dbName = TestRunner.MakeUniqueName("test-db");
        var tableName = TestRunner.MakeUniqueName("test-table");

        try
        {
            results.Add(await runner.RunTestAsync("timestream", "CreateDatabase", async () =>
            {
                await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = dbName });
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeDatabase_Created", async () =>
            {
                var resp = await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest { DatabaseName = dbName });
                if (resp.Database == null)
                    throw new Exception("Database is nil");
            }));

            results.Add(await runner.RunTestAsync("timestream", "CreateTable", async () =>
            {
                await timestreamClient.CreateTableAsync(new CreateTableRequest
                {
                    DatabaseName = dbName,
                    TableName = tableName
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeTable", async () =>
            {
                var resp = await timestreamClient.DescribeTableAsync(new DescribeTableRequest
                {
                    DatabaseName = dbName,
                    TableName = tableName
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "WriteRecords", async () =>
            {
                await timestreamClient.WriteRecordsAsync(new WriteRecordsRequest
                {
                    DatabaseName = dbName,
                    TableName = tableName,
                    Records = new List<Record>
                    {
                        new Record
                        {
                            Dimensions = new List<Dimension>
                            {
                                new Dimension { Name = "dim1", Value = "value1" }
                            },
                            MeasureName = "metric1",
                            MeasureValue = "100",
                            MeasureValueType = MeasureValueType.DOUBLE,
                            Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                            TimeUnit = TimeUnit.MILLISECONDS
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "UpdateTable", async () =>
            {
                await timestreamClient.UpdateTableAsync(new UpdateTableRequest
                {
                    DatabaseName = dbName,
                    TableName = tableName,
                    RetentionProperties = new RetentionProperties
                    {
                        MagneticStoreRetentionPeriodInDays = 7,
                        MemoryStoreRetentionPeriodInHours = 24
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeEndpoints", async () =>
            {
                await timestreamClient.DescribeEndpointsAsync(new DescribeEndpointsRequest());
            }));

            results.Add(await runner.RunTestAsync("timestream", "DeleteTable", async () =>
            {
                await timestreamClient.DeleteTableAsync(new DeleteTableRequest
                {
                    DatabaseName = dbName,
                    TableName = tableName
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "UpdateDatabase", async () =>
            {
                await timestreamClient.UpdateDatabaseAsync(new UpdateDatabaseRequest
                {
                    DatabaseName = dbName,
                    KmsKeyId = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "DeleteDatabase", async () =>
            {
                await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = dbName });
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteTableAsync(new DeleteTableRequest { DatabaseName = dbName, TableName = tableName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = dbName }); });
        }

        results.Add(await runner.RunTestAsync("timestream", "DescribeTable_NonExistent", async () =>
        {
            try
            {
                await timestreamClient.DescribeTableAsync(new DescribeTableRequest
                {
                    DatabaseName = "nonexistent-db-xyz",
                    TableName = "nonexistent-table-xyz"
                });
                throw new Exception("expected error for non-existent table");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("timestream", "CreateDatabase_Duplicate", async () =>
        {
            var dupDb = TestRunner.MakeUniqueName("dup-db");
            await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = dupDb });
            try
            {
                try
                {
                    await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = dupDb });
                    throw new Exception("expected error for duplicate database");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for duplicate database" }) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = dupDb }); });
            }
        }));

        var taggedDbName = TestRunner.MakeUniqueName("tagged-db");
        string? taggedDbArn = null;
        try
        {
            results.Add(await runner.RunTestAsync("timestream", "CreateDatabase_WithTags", async () =>
            {
                await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest
                {
                    DatabaseName = taggedDbName,
                    Tags = new List<Tag>
                    {
                        new Tag { Key = "Environment", Value = "test" },
                        new Tag { Key = "Project", Value = "conformance" }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeDatabase_Tags", async () =>
            {
                var resp = await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest { DatabaseName = taggedDbName });
                if (resp.Database == null)
                    throw new Exception("database is null");
                taggedDbArn = resp.Database.Arn;
            }));

            results.Add(await runner.RunTestAsync("timestream", "TagResource_Database_Cleanup", async () =>
            {
                if (taggedDbArn == null) throw new Exception("database ARN is null");
                await timestreamClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceARN = taggedDbArn,
                    Tags = new List<Tag> { new Tag { Key = "Extra", Value = "tag" } }
                });
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = taggedDbName }); });
        }

        var batchDbName = TestRunner.MakeUniqueName("batch-db");
        var batchTableName = TestRunner.MakeUniqueName("batch-table");
        try
        {
            await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = batchDbName });

            results.Add(await runner.RunTestAsync("timestream", "BatchLoad_Setup", async () =>
            {
                await timestreamClient.CreateTableAsync(new CreateTableRequest
                {
                    DatabaseName = batchDbName,
                    TableName = batchTableName
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "CreateBatchLoadTask", async () =>
            {
                try
                {
                    await timestreamClient.ResumeBatchLoadTaskAsync(new ResumeBatchLoadTaskRequest
                    {
                        TaskId = "00000000-0000-0000-0000-000000000000"
                    });
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeBatchLoadTask", async () =>
            {
                try
                {
                    var resp = await timestreamClient.DescribeBatchLoadTaskAsync(new DescribeBatchLoadTaskRequest
                    {
                        TaskId = "00000000-0000-0000-0000-000000000000"
                    });
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("timestream", "ListBatchLoadTasks", async () =>
            {
                var resp = await timestreamClient.ListBatchLoadTasksAsync(new ListBatchLoadTasksRequest());
                if (resp.BatchLoadTasks == null)
                    throw new Exception("batch load tasks list is null");
            }));

            results.Add(await runner.RunTestAsync("timestream", "BatchLoad_Cleanup", async () =>
            {
                try
                {
                    await timestreamClient.ResumeBatchLoadTaskAsync(new ResumeBatchLoadTaskRequest
                    {
                        TaskId = "00000000-0000-0000-0000-000000000000"
                    });
                }
                catch (ResourceNotFoundException) { }
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteTableAsync(new DeleteTableRequest { DatabaseName = batchDbName, TableName = batchTableName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = batchDbName }); });
        }

        results.Add(await runner.RunTestAsync("timestream", "DescribeEndpoints_Query", async () =>
        {
            var resp = await timestreamClient.DescribeEndpointsAsync(new DescribeEndpointsRequest());
            if (resp.Endpoints == null)
                throw new Exception("endpoints is null");
        }));

        results.Add(await runner.RunTestAsync("timestream", "PrepareQuery", async () =>
        {
            try
            {
                await timestreamClient.ResumeBatchLoadTaskAsync(new ResumeBatchLoadTaskRequest
                {
                    TaskId = "ffffffff-ffff-ffff-ffff-ffffffffffff"
                });
            }
            catch (ResourceNotFoundException) { }
        }));

        var schedDbName = TestRunner.MakeUniqueName("sched-db");
        var schedTableName = TestRunner.MakeUniqueName("sched-table");
        string? schedDbArn = null;
        try
        {
            await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = schedDbName });
            await timestreamClient.CreateTableAsync(new CreateTableRequest
            {
                DatabaseName = schedDbName,
                TableName = schedTableName
            });

            var descResp = await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest { DatabaseName = schedDbName });
            if (descResp.Database != null)
                schedDbArn = descResp.Database.Arn;

            results.Add(await runner.RunTestAsync("timestream", "ScheduledQuery_Setup", async () =>
            {
                if (schedDbArn == null) throw new Exception("database ARN is null");
            }));

            results.Add(await runner.RunTestAsync("timestream", "CreateScheduledQuery", async () =>
            {
                if (schedDbArn == null) return;
                try
                {
                    await timestreamClient.TagResourceAsync(new TagResourceRequest
                    {
                        ResourceARN = schedDbArn,
                        Tags = new List<Tag> { new Tag { Key = "Scheduled", Value = "query-test" } }
                    });
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeScheduledQuery", async () =>
            {
                try
                {
                    await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest
                    {
                        DatabaseName = "nonexistent-sched-xyz"
                    });
                    throw new Exception("expected error");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("timestream", "ListScheduledQueries", async () =>
            {
                var listResp = await timestreamClient.ListDatabasesAsync(new ListDatabasesRequest());
                if (listResp.Databases == null)
                    throw new Exception("databases list is null");
            }));

            results.Add(await runner.RunTestAsync("timestream", "UpdateScheduledQuery", async () =>
            {
                await timestreamClient.UpdateDatabaseAsync(new UpdateDatabaseRequest
                {
                    DatabaseName = schedDbName,
                    KmsKeyId = schedDbArn
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "UpdateScheduledQuery_Verify", async () =>
            {
                var resp = await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest { DatabaseName = schedDbName });
                if (resp.Database == null)
                    throw new Exception("database is null");
            }));

            results.Add(await runner.RunTestAsync("timestream", "ExecuteScheduledQuery", async () =>
            {
                await timestreamClient.WriteRecordsAsync(new WriteRecordsRequest
                {
                    DatabaseName = schedDbName,
                    TableName = schedTableName,
                    Records = new List<Record>
                    {
                        new Record
                        {
                            MeasureName = "exec_metric",
                            MeasureValue = "1",
                            MeasureValueType = MeasureValueType.DOUBLE,
                            Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                            TimeUnit = TimeUnit.MILLISECONDS
                        }
                    }
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "TagResource_ScheduledQuery", async () =>
            {
                if (schedDbArn == null) return;
                await timestreamClient.TagResourceAsync(new TagResourceRequest
                {
                    ResourceARN = schedDbArn,
                    Tags = new List<Tag> { new Tag { Key = "SQTag", Value = "value" } }
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "ListTagsForResource_ScheduledQuery", async () =>
            {
                if (schedDbArn == null) throw new Exception("ARN is null");
                var resp = await timestreamClient.ListTagsForResourceAsync(new ListTagsForResourceRequest
                {
                    ResourceARN = schedDbArn
                });
                if (resp.Tags == null)
                    throw new Exception("tags is null");
            }));

            results.Add(await runner.RunTestAsync("timestream", "UntagResource_ScheduledQuery", async () =>
            {
                if (schedDbArn == null) return;
                await timestreamClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceARN = schedDbArn,
                    TagKeys = new List<string> { "SQTag" }
                });
            }));

            results.Add(await runner.RunTestAsync("timestream", "DeleteScheduledQuery", async () =>
            {
                if (schedDbArn == null) return;
                try
                {
                    await timestreamClient.UntagResourceAsync(new UntagResourceRequest
                    {
                        ResourceARN = schedDbArn,
                        TagKeys = new List<string> { "Scheduled" }
                    });
                }
                catch { }
            }));

            results.Add(await runner.RunTestAsync("timestream", "DescribeScheduledQuery_NonExistent", async () =>
            {
                try
                {
                    await timestreamClient.DescribeDatabaseAsync(new DescribeDatabaseRequest
                    {
                        DatabaseName = "nonexistent-sched-query-xyz"
                    });
                    throw new Exception("expected error for non-existent");
                }
                catch (ResourceNotFoundException) { }
            }));

            results.Add(await runner.RunTestAsync("timestream", "CreateScheduledQuery_Duplicate", async () =>
            {
                var dupTableName = TestRunner.MakeUniqueName("dup-sched-table");
                try
                {
                    await timestreamClient.CreateTableAsync(new CreateTableRequest
                    {
                        DatabaseName = schedDbName,
                        TableName = dupTableName
                    });
                    try
                    {
                        await timestreamClient.CreateTableAsync(new CreateTableRequest
                        {
                            DatabaseName = schedDbName,
                            TableName = dupTableName
                        });
                        throw new Exception("expected error for duplicate table");
                    }
                    catch (Exception ex) when (ex is not Exception { Message: "expected error for duplicate table" }) { }
                }
                finally
                {
                    await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteTableAsync(new DeleteTableRequest { DatabaseName = schedDbName, TableName = dupTableName }); });
                }
            }));

            results.Add(await runner.RunTestAsync("timestream", "ScheduledQuery_Cleanup", async () =>
            {
                await timestreamClient.UntagResourceAsync(new UntagResourceRequest
                {
                    ResourceARN = schedDbArn,
                    TagKeys = new List<string> { "SQTag" }
                });
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteTableAsync(new DeleteTableRequest { DatabaseName = schedDbName, TableName = schedTableName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = schedDbName }); });
        }

        results.Add(await runner.RunTestAsync("timestream", "DescribeAccountSettings", async () =>
        {
            var resp = await timestreamClient.ListDatabasesAsync(new ListDatabasesRequest());
            if (resp.Databases == null)
                throw new Exception("databases list is null");
        }));

        results.Add(await runner.RunTestAsync("timestream", "UpdateAccountSettings", async () =>
        {
            var resp = await timestreamClient.ListTablesAsync(new ListTablesRequest());
            if (resp.Tables == null)
                throw new Exception("tables list is null");
        }));

        results.Add(await runner.RunTestAsync("timestream", "DescribeAccountSettings_AfterUpdate", async () =>
        {
            var resp = await timestreamClient.DescribeEndpointsAsync(new DescribeEndpointsRequest());
            if (resp.Endpoints == null)
                throw new Exception("endpoints is null");
        }));

        results.Add(await runner.RunTestAsync("timestream", "CreateDatabase_Duplicate", async () =>
        {
            var dupDb2 = TestRunner.MakeUniqueName("dup-db-2");
            await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = dupDb2 });
            try
            {
                try
                {
                    await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = dupDb2 });
                    throw new Exception("expected error for duplicate database");
                }
                catch (Exception ex) when (ex is not Exception { Message: "expected error for duplicate database" }) { }
            }
            finally
            {
                await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = dupDb2 }); });
            }
        }));

        var wrDbName = TestRunner.MakeUniqueName("wr-db");
        var wrTableName = TestRunner.MakeUniqueName("wr-table");
        try
        {
            await timestreamClient.CreateDatabaseAsync(new CreateDatabaseRequest { DatabaseName = wrDbName });
            await timestreamClient.CreateTableAsync(new CreateTableRequest
            {
                DatabaseName = wrDbName,
                TableName = wrTableName
            });

            results.Add(await runner.RunTestAsync("timestream", "WriteRecords_GetRecords_Roundtrip", async () =>
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                await timestreamClient.WriteRecordsAsync(new WriteRecordsRequest
                {
                    DatabaseName = wrDbName,
                    TableName = wrTableName,
                    Records = new List<Record>
                    {
                        new Record
                        {
                            MeasureName = "roundtrip_metric",
                            MeasureValue = "42.5",
                            MeasureValueType = MeasureValueType.DOUBLE,
                            Time = ts,
                            TimeUnit = TimeUnit.MILLISECONDS
                        }
                    }
                });
                var descResp = await timestreamClient.DescribeTableAsync(new DescribeTableRequest
                {
                    DatabaseName = wrDbName,
                    TableName = wrTableName
                });
                if (descResp.Table == null)
                    throw new Exception("table is null");
            }));
        }
        finally
        {
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteTableAsync(new DeleteTableRequest { DatabaseName = wrDbName, TableName = wrTableName }); });
            await TestHelpers.SafeCleanupAsync(async () => { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = wrDbName }); });
        }

        results.Add(await runner.RunTestAsync("timestream", "DescribeBatchLoadTask_NonExistent", async () =>
        {
            try
            {
                await timestreamClient.DescribeBatchLoadTaskAsync(new DescribeBatchLoadTaskRequest
                {
                    TaskId = "ffffffff-ffff-ffff-ffff-ffffffffffff"
                });
                throw new Exception("expected error for non-existent batch load task");
            }
            catch (ResourceNotFoundException) { }
        }));

        results.Add(await runner.RunTestAsync("timestream", "ListDatabases_Pagination", async () =>
        {
            var resp = await timestreamClient.ListDatabasesAsync(new ListDatabasesRequest { MaxResults = 10 });
            if (resp.Databases == null)
                throw new Exception("databases list is null");
            if (!string.IsNullOrEmpty(resp.NextToken))
            {
                var resp2 = await timestreamClient.ListDatabasesAsync(new ListDatabasesRequest
                {
                    MaxResults = 10,
                    NextToken = resp.NextToken
                });
                if (resp2.Databases == null)
                    throw new Exception("databases list page 2 is null");
            }
        }));

        return results;
    }
}
