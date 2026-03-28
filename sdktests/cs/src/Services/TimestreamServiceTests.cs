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
            try { await timestreamClient.DeleteTableAsync(new DeleteTableRequest { DatabaseName = dbName, TableName = tableName }); } catch { }
            try { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = dbName }); } catch { }
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
                try { await timestreamClient.DeleteDatabaseAsync(new DeleteDatabaseRequest { DatabaseName = dupDb }); } catch { }
            }
        }));

        return results;
    }
}
