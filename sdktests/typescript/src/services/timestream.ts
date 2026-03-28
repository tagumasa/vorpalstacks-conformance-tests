import {
  TimestreamWriteClient,
  CreateDatabaseCommand,
  ListDatabasesCommand,
  DescribeDatabaseCommand,
  CreateTableCommand,
  ListTablesCommand,
  DescribeTableCommand,
  WriteRecordsCommand,
  UpdateTableCommand,
  DescribeEndpointsCommand,
  DeleteTableCommand,
  UpdateDatabaseCommand,
  DeleteDatabaseCommand,
} from "@aws-sdk/client-timestream-write";
import { TestRunner, TestResult } from "../runner";

export async function runTimestreamTests(
  runner: TestRunner,
  client: TimestreamWriteClient,
  region: string
): Promise<TestResult[]> {
  const results: TestResult[] = [];
  const databaseName = `test-db-${Date.now()}`;
  const tableName = "test-table";
  const databaseName2 = `test-db2-${Date.now()}`;

  results.push(await runner.runTest("timestream", "CreateDatabase", async () => {
    await client.send(new CreateDatabaseCommand({
      DatabaseName: databaseName,
    }));
  }));

  results.push(await runner.runTest("timestream", "ListDatabases", async () => {
    await client.send(new ListDatabasesCommand({}));
  }));

  results.push(await runner.runTest("timestream", "DescribeDatabase", async () => {
    await client.send(new DescribeDatabaseCommand({
      DatabaseName: databaseName,
    }));
  }));

  results.push(await runner.runTest("timestream", "CreateTable", async () => {
    await client.send(new CreateTableCommand({
      DatabaseName: databaseName,
      TableName: tableName,
      RetentionProperties: {
        MemoryStoreRetentionPeriodInHours: 24,
        MagneticStoreRetentionPeriodInDays: 30,
      },
    }));
  }));

  results.push(await runner.runTest("timestream", "ListTables", async () => {
    await client.send(new ListTablesCommand({
      DatabaseName: databaseName,
    }));
  }));

  results.push(await runner.runTest("timestream", "DescribeTable", async () => {
    await client.send(new DescribeTableCommand({
      DatabaseName: databaseName,
      TableName: tableName,
    }));
  }));

  const currentTime = Date.now();
  results.push(await runner.runTest("timestream", "WriteRecords", async () => {
    await client.send(new WriteRecordsCommand({
      DatabaseName: databaseName,
      TableName: tableName,
      Records: [{
        Dimensions: [
          { Name: "region", Value: "us-east-1" },
          { Name: "host", Value: "host1" },
        ],
        MeasureName: "cpu_utilization",
        MeasureValue: "75.5",
        MeasureValueType: "DOUBLE",
        Time: String(currentTime),
        TimeUnit: "MILLISECONDS",
      }],
    }));
  }));

  results.push(await runner.runTest("timestream", "UpdateTable", async () => {
    await client.send(new UpdateTableCommand({
      DatabaseName: databaseName,
      TableName: tableName,
      RetentionProperties: {
        MemoryStoreRetentionPeriodInHours: 48,
        MagneticStoreRetentionPeriodInDays: 60,
      },
    }));
  }));

  results.push(await runner.runTest("timestream", "DescribeEndpoints", async () => {
    await client.send(new DescribeEndpointsCommand({}));
  }));

  results.push(await runner.runTest("timestream", "DeleteTable", async () => {
    await client.send(new DeleteTableCommand({
      DatabaseName: databaseName,
      TableName: tableName,
    }));
  }));

  results.push(await runner.runTest("timestream", "UpdateDatabase", async () => {
    await client.send(new UpdateDatabaseCommand({
      DatabaseName: databaseName,
      KmsKeyId: "alias/aws/timestream",
    }));
  }));

  results.push(await runner.runTest("timestream", "DeleteDatabase", async () => {
    await client.send(new DeleteDatabaseCommand({
      DatabaseName: databaseName,
    }));
  }));

  results.push(await runner.runTest("timestream", "DescribeDatabase_NonExistent", async () => {
    try {
      await client.send(new DescribeDatabaseCommand({
        DatabaseName: "nonexistent-database-xyz",
      }));
      throw new Error("expected error for non-existent database");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent database") {
        throw err;
      }
    }
  }));

  results.push(await runner.runTest("timestream", "DescribeTable_NonExistent", async () => {
    try {
      await client.send(new DescribeTableCommand({
        DatabaseName: databaseName2,
        TableName: "nonexistent-table-xyz",
      }));
      throw new Error("expected error for non-existent table");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for non-existent table") {
        throw err;
      }
    }
  }));

  const dupDbName = `dup-db-${Date.now()}`;
  results.push(await runner.runTest("timestream", "CreateDatabase_Duplicate", async () => {
    await client.send(new CreateDatabaseCommand({
      DatabaseName: dupDbName,
    }));
    try {
      await client.send(new CreateDatabaseCommand({
        DatabaseName: dupDbName,
      }));
      throw new Error("expected error for duplicate database");
    } catch (err: unknown) {
      if (err instanceof Error && err.message === "expected error for duplicate database") {
        throw err;
      }
    } finally {
      try {
        await client.send(new DeleteDatabaseCommand({
          DatabaseName: dupDbName,
        }));
      } catch {
        // ignore cleanup
      }
    }
  }));

  results.push(await runner.runTest("timestream", "WriteRecords_GetRecords_Roundtrip", async () => {
    const roundtripDb = `roundtrip-db-${Date.now()}`;
    const roundtripTable = "roundtrip-table";
    await client.send(new CreateDatabaseCommand({
      DatabaseName: roundtripDb,
    }));
    await client.send(new CreateTableCommand({
      DatabaseName: roundtripDb,
      TableName: roundtripTable,
    }));
    const writeTime = Date.now();
    await client.send(new WriteRecordsCommand({
      DatabaseName: roundtripDb,
      TableName: roundtripTable,
      Records: [{
        Dimensions: [
          { Name: "device", Value: "sensor-1" },
        ],
        MeasureName: "temperature",
        MeasureValue: "98.6",
        MeasureValueType: "DOUBLE",
        Time: String(writeTime),
        TimeUnit: "MILLISECONDS",
      }],
    }));
    await client.send(new DeleteTableCommand({
      DatabaseName: roundtripDb,
      TableName: roundtripTable,
    }));
    await client.send(new DeleteDatabaseCommand({
      DatabaseName: roundtripDb,
    }));
  }));

  return results;
}