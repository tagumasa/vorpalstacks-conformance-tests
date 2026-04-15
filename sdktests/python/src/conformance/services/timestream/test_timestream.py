import time

import pytest


@pytest.fixture(scope="class")
def database(timestream_client, unique_name):
    db_name = unique_name("db")
    timestream_client.create_database(DatabaseName=db_name)
    yield db_name
    try:
        timestream_client.delete_database(DatabaseName=db_name)
    except Exception:
        pass


@pytest.fixture(scope="class")
def table(timestream_client, database):
    table_name = "test-table"
    timestream_client.create_table(
        DatabaseName=database,
        TableName=table_name,
        RetentionProperties={
            "MemoryStoreRetentionPeriodInHours": 24,
            "MagneticStoreRetentionPeriodInDays": 30,
        },
    )
    yield database, table_name
    try:
        timestream_client.delete_table(DatabaseName=database, TableName=table_name)
    except Exception:
        pass


class TestCreateDatabase:
    def test_create_database(self, timestream_client, unique_name):
        db_name = unique_name("db")
        timestream_client.create_database(DatabaseName=db_name)
        try:
            timestream_client.delete_database(DatabaseName=db_name)
        except Exception:
            pass

    def test_duplicate(self, timestream_client, unique_name):
        dup_db_name = unique_name("dup")
        timestream_client.create_database(DatabaseName=dup_db_name)
        try:
            with pytest.raises(Exception):
                timestream_client.create_database(DatabaseName=dup_db_name)
        finally:
            try:
                timestream_client.delete_database(DatabaseName=dup_db_name)
            except Exception:
                pass


class TestListDatabases:
    def test_list_databases(self, timestream_client):
        resp = timestream_client.list_databases()
        assert resp.get("Databases") is not None


class TestDescribeDatabase:
    def test_describe_database(self, timestream_client, database):
        timestream_client.describe_database(DatabaseName=database)

    def test_nonexistent(self, timestream_client):
        with pytest.raises(Exception):
            timestream_client.describe_database(DatabaseName="nonexistent-database-xyz")


class TestCreateTable:
    def test_create_table(self, timestream_client, database):
        timestream_client.create_table(
            DatabaseName=database,
            TableName="test-table",
            RetentionProperties={
                "MemoryStoreRetentionPeriodInHours": 24,
                "MagneticStoreRetentionPeriodInDays": 30,
            },
        )


class TestListTables:
    def test_list_tables(self, timestream_client, database):
        resp = timestream_client.list_tables(DatabaseName=database)
        assert resp.get("Tables") is not None


class TestDescribeTable:
    def test_describe_table(self, timestream_client, table):
        db_name, tbl_name = table
        timestream_client.describe_table(DatabaseName=db_name, TableName=tbl_name)

    def test_nonexistent(self, timestream_client, unique_name):
        db_name = unique_name("db2")
        with pytest.raises(Exception):
            timestream_client.describe_table(
                DatabaseName=db_name,
                TableName="nonexistent-table-xyz",
            )


class TestWriteRecords:
    def test_write_records(self, timestream_client, table):
        db_name, tbl_name = table
        current_time = int(time.time() * 1000)
        timestream_client.write_records(
            DatabaseName=db_name,
            TableName=tbl_name,
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

    def test_roundtrip(self, timestream_client, unique_name):
        roundtrip_db = unique_name("rtdb")
        roundtrip_table = "roundtrip-table"
        timestream_client.create_database(DatabaseName=roundtrip_db)
        timestream_client.create_table(
            DatabaseName=roundtrip_db, TableName=roundtrip_table
        )
        write_time = int(time.time() * 1000)
        timestream_client.write_records(
            DatabaseName=roundtrip_db,
            TableName=roundtrip_table,
            Records=[
                {
                    "Dimensions": [{"Name": "device", "Value": "sensor-1"}],
                    "MeasureName": "temperature",
                    "MeasureValue": "98.6",
                    "MeasureValueType": "DOUBLE",
                    "Time": str(write_time),
                    "TimeUnit": "MILLISECONDS",
                },
            ],
        )
        timestream_client.delete_table(
            DatabaseName=roundtrip_db, TableName=roundtrip_table
        )
        timestream_client.delete_database(DatabaseName=roundtrip_db)


class TestUpdateTable:
    def test_update_table(self, timestream_client, table):
        db_name, tbl_name = table
        timestream_client.update_table(
            DatabaseName=db_name,
            TableName=tbl_name,
            RetentionProperties={
                "MemoryStoreRetentionPeriodInHours": 48,
                "MagneticStoreRetentionPeriodInDays": 60,
            },
        )


class TestDescribeEndpoints:
    def test_describe_endpoints(self, timestream_client):
        timestream_client.describe_endpoints()


class TestUpdateDatabase:
    def test_update_database(self, timestream_client, database):
        timestream_client.update_database(
            DatabaseName=database,
            KmsKeyId="alias/aws/timestream",
        )


class TestDeleteTable:
    def test_delete_table(self, timestream_client, database):
        tbl_name = "del-table"
        timestream_client.create_table(DatabaseName=database, TableName=tbl_name)
        timestream_client.delete_table(DatabaseName=database, TableName=tbl_name)


class TestDeleteDatabase:
    def test_delete_database(self, timestream_client, unique_name):
        db_name = unique_name("ddb")
        timestream_client.create_database(DatabaseName=db_name)
        timestream_client.delete_database(DatabaseName=db_name)
