import time
from ..runner import TestRunner, TestResult


async def run_timestream_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    client = session.client(
        "timestream-write", endpoint_url=endpoint, region_name=region
    )

    database_name = f"test-db-{int(time.time() * 1000)}"
    table_name = "test-table"
    database_name2 = f"test-db2-{int(time.time() * 1000)}"

    def _create_database():
        client.create_database(DatabaseName=database_name)

    results.append(
        await runner.run_test("timestream", "CreateDatabase", _create_database)
    )

    def _list_databases():
        resp = client.list_databases()
        assert resp.get("Databases") is not None

    results.append(
        await runner.run_test("timestream", "ListDatabases", _list_databases)
    )

    def _describe_database():
        client.describe_database(DatabaseName=database_name)

    results.append(
        await runner.run_test("timestream", "DescribeDatabase", _describe_database)
    )

    def _create_table():
        client.create_table(
            DatabaseName=database_name,
            TableName=table_name,
            RetentionProperties={
                "MemoryStoreRetentionPeriodInHours": 24,
                "MagneticStoreRetentionPeriodInDays": 30,
            },
        )

    results.append(await runner.run_test("timestream", "CreateTable", _create_table))

    def _list_tables():
        resp = client.list_tables(DatabaseName=database_name)
        assert resp.get("Tables") is not None

    results.append(await runner.run_test("timestream", "ListTables", _list_tables))

    def _describe_table():
        client.describe_table(DatabaseName=database_name, TableName=table_name)

    results.append(
        await runner.run_test("timestream", "DescribeTable", _describe_table)
    )

    current_time = int(time.time() * 1000)

    def _write_records():
        client.write_records(
            DatabaseName=database_name,
            TableName=table_name,
            Records=[
                {
                    "Dimensions": [
                        {"Name": "region", "Value": "us-east-1"},
                        {"Name": "host", "Value": "host1"},
                    ],
                    "MeasureName": "cpu_utilization",
                    "MeasureValue": "75.5",
                    "MeasureValueType": "DOUBLE",
                    "Time": str(current_time),
                    "TimeUnit": "MILLISECONDS",
                },
            ],
        )

    results.append(await runner.run_test("timestream", "WriteRecords", _write_records))

    def _update_table():
        client.update_table(
            DatabaseName=database_name,
            TableName=table_name,
            RetentionProperties={
                "MemoryStoreRetentionPeriodInHours": 48,
                "MagneticStoreRetentionPeriodInDays": 60,
            },
        )

    results.append(await runner.run_test("timestream", "UpdateTable", _update_table))

    def _describe_endpoints():
        client.describe_endpoints()

    results.append(
        await runner.run_test("timestream", "DescribeEndpoints", _describe_endpoints)
    )

    def _delete_table():
        client.delete_table(DatabaseName=database_name, TableName=table_name)

    results.append(await runner.run_test("timestream", "DeleteTable", _delete_table))

    def _update_database():
        client.update_database(
            DatabaseName=database_name,
            KmsKeyId="alias/aws/timestream",
        )

    results.append(
        await runner.run_test("timestream", "UpdateDatabase", _update_database)
    )

    def _delete_database():
        client.delete_database(DatabaseName=database_name)

    results.append(
        await runner.run_test("timestream", "DeleteDatabase", _delete_database)
    )

    def _describe_database_nonexistent():
        try:
            client.describe_database(DatabaseName="nonexistent-database-xyz")
            raise Exception("expected error for non-existent database")
        except Exception as e:
            if str(e) == "expected error for non-existent database":
                raise

    results.append(
        await runner.run_test(
            "timestream", "DescribeDatabase_NonExistent", _describe_database_nonexistent
        )
    )

    def _describe_table_nonexistent():
        try:
            client.describe_table(
                DatabaseName=database_name2,
                TableName="nonexistent-table-xyz",
            )
            raise Exception("expected error for non-existent table")
        except Exception as e:
            if str(e) == "expected error for non-existent table":
                raise

    results.append(
        await runner.run_test(
            "timestream", "DescribeTable_NonExistent", _describe_table_nonexistent
        )
    )

    dup_db_name = f"dup-db-{int(time.time() * 1000)}"

    def _create_database_duplicate():
        client.create_database(DatabaseName=dup_db_name)
        try:
            client.create_database(DatabaseName=dup_db_name)
            raise Exception("expected error for duplicate database")
        except Exception as e:
            if str(e) == "expected error for duplicate database":
                raise
        finally:
            try:
                client.delete_database(DatabaseName=dup_db_name)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "timestream", "CreateDatabase_Duplicate", _create_database_duplicate
        )
    )

    def _write_records_get_records_roundtrip():
        roundtrip_db = f"roundtrip-db-{int(time.time() * 1000)}"
        roundtrip_table = "roundtrip-table"
        client.create_database(DatabaseName=roundtrip_db)
        client.create_table(
            DatabaseName=roundtrip_db,
            TableName=roundtrip_table,
        )
        write_time = int(time.time() * 1000)
        client.write_records(
            DatabaseName=roundtrip_db,
            TableName=roundtrip_table,
            Records=[
                {
                    "Dimensions": [
                        {"Name": "device", "Value": "sensor-1"},
                    ],
                    "MeasureName": "temperature",
                    "MeasureValue": "98.6",
                    "MeasureValueType": "DOUBLE",
                    "Time": str(write_time),
                    "TimeUnit": "MILLISECONDS",
                },
            ],
        )
        client.delete_table(DatabaseName=roundtrip_db, TableName=roundtrip_table)
        client.delete_database(DatabaseName=roundtrip_db)

    results.append(
        await runner.run_test(
            "timestream",
            "WriteRecords_GetRecords_Roundtrip",
            _write_records_get_records_roundtrip,
        )
    )

    return results
