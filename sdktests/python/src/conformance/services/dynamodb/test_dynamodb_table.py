import pytest


@pytest.fixture(scope="module")
def dynamodb_table(dynamodb_client, region, unique_name):
    table_name = unique_name("PyTable")
    dynamodb_client.create_table(
        TableName=table_name,
        AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
        KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
        BillingMode="PAY_PER_REQUEST",
    )
    yield {
        "name": table_name,
        "arn": f"arn:aws:dynamodb:{region}:000000000000:table/{table_name}",
    }
    try:
        dynamodb_client.delete_table(TableName=table_name)
    except Exception:
        pass


class TestCreateTable:
    def test_create_table(self, dynamodb_table):
        assert dynamodb_table["name"]


class TestDescribeTable:
    def test_describe_table(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.describe_table(TableName=dynamodb_table["name"])
        assert resp.get("Table")


class TestListTables:
    def test_list_tables(self, dynamodb_client):
        resp = dynamodb_client.list_tables()
        assert resp.get("TableNames") is not None


class TestUpdateTable:
    def test_update_table(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.update_table(TableName=dynamodb_table["name"])
        assert resp.get("TableDescription")


class TestDeleteTable:
    def test_delete_table(self, dynamodb_client, unique_name):
        table_name = unique_name("PyDelTable")
        dynamodb_client.create_table(
            TableName=table_name,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        dynamodb_client.delete_table(TableName=table_name)


class TestUpdateTimeToLive:
    def test_update_time_to_live(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.update_time_to_live(
            TableName=dynamodb_table["name"],
            TimeToLiveSpecification={"AttributeName": "ttl", "Enabled": True},
        )
        assert resp.get("TimeToLiveSpecification")


class TestDescribeTimeToLive:
    def test_describe_time_to_live(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.describe_time_to_live(TableName=dynamodb_table["name"])
        assert resp.get("TimeToLiveDescription")


class TestBackupOperations:
    def test_create_backup(self, dynamodb_client, dynamodb_table, unique_name):
        backup_name = unique_name("PyBackup")
        resp = dynamodb_client.create_backup(
            TableName=dynamodb_table["name"], BackupName=backup_name
        )
        assert resp.get("BackupDetails")

    def test_list_backups(self, dynamodb_client):
        resp = dynamodb_client.list_backups()
        assert resp.get("BackupSummaries") is not None


class TestContinuousBackups:
    def test_describe_continuous_backups(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.describe_continuous_backups(
            TableName=dynamodb_table["name"]
        )
        assert resp.get("ContinuousBackupsDescription")

    def test_update_continuous_backups(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.update_continuous_backups(
            TableName=dynamodb_table["name"],
            PointInTimeRecoverySpecification={"PointInTimeRecoveryEnabled": True},
        )
        assert resp.get("ContinuousBackupsDescription")
