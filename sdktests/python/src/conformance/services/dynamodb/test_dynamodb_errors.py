import pytest
from botocore.exceptions import ClientError
from conformance.conftest import assert_client_error


class TestNonExistentTable:
    def test_get_item_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.get_item(
                TableName="NoSuchTable_xyz", Key={"id": {"S": "test1"}}
            )
        assert_client_error(exc, "ResourceNotFoundException")

    def test_put_item_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.put_item(
                TableName="NoSuchTable_xyz", Item={"id": {"S": "k"}}
            )
        assert_client_error(exc, "ResourceNotFoundException")

    def test_query_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.query(
                TableName="NoSuchTable_xyz",
                KeyConditionExpression="id = :id",
                ExpressionAttributeValues={":id": {"S": "k"}},
            )
        assert_client_error(exc, "ResourceNotFoundException")

    def test_scan_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.scan(TableName="NoSuchTable_xyz")
        assert_client_error(exc, "ResourceNotFoundException")

    def test_describe_table_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.describe_table(TableName="NoSuchTable_xyz")
        assert_client_error(exc, "ResourceNotFoundException")

    def test_delete_table_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.delete_table(TableName="NoSuchTable_xyz")
        assert_client_error(exc, "ResourceNotFoundException")

    def test_batch_get_item_nonexistent_table(self, dynamodb_client):
        with pytest.raises(ClientError) as exc:
            dynamodb_client.batch_get_item(
                RequestItems={"NonExistentTable_xyz": {"Keys": [{"id": {"S": "k"}}]}}
            )
        assert_client_error(exc, "ResourceNotFoundException")


class TestConditionalCheckFail:
    def test_update_item_conditional_check_fail(self, dynamodb_client, unique_name):
        err_table = unique_name("CondTable")
        dynamodb_client.create_table(
            TableName=err_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb_client.put_item(
                TableName=err_table,
                Item={"id": {"S": "cond1"}, "status": {"S": "active"}},
            )
            with pytest.raises(ClientError) as exc:
                dynamodb_client.update_item(
                    TableName=err_table,
                    Key={"id": {"S": "cond1"}},
                    UpdateExpression="SET #s = :val",
                    ConditionExpression="#s = :expected",
                    ExpressionAttributeNames={"#s": "status"},
                    ExpressionAttributeValues={
                        ":val": {"S": "inactive"},
                        ":expected": {"S": "deleted"},
                    },
                )
            assert_client_error(exc, "ConditionalCheckFailedException")
        finally:
            try:
                dynamodb_client.delete_table(TableName=err_table)
            except Exception:
                pass

    def test_delete_item_conditional_check_fail(self, dynamodb_client, unique_name):
        err_table = unique_name("DelCondTable")
        dynamodb_client.create_table(
            TableName=err_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb_client.put_item(
                TableName=err_table,
                Item={"id": {"S": "del1"}, "status": {"S": "active"}},
            )
            with pytest.raises(ClientError) as exc:
                dynamodb_client.delete_item(
                    TableName=err_table,
                    Key={"id": {"S": "del1"}},
                    ConditionExpression="attribute_not_exists(id)",
                )
            assert_client_error(exc, "ConditionalCheckFailedException")
        finally:
            try:
                dynamodb_client.delete_table(TableName=err_table)
            except Exception:
                pass


class TestGetItemNonExistentKey:
    def test_get_item_nonexistent_key(self, dynamodb_client, unique_name):
        err_table = unique_name("GetItemErr")
        dynamodb_client.create_table(
            TableName=err_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            resp = dynamodb_client.get_item(
                TableName=err_table, Key={"id": {"S": "nonexistent"}}
            )
            assert len(resp.get("Item", {})) == 0
        finally:
            try:
                dynamodb_client.delete_table(TableName=err_table)
            except Exception:
                pass


class TestCreateTableDuplicate:
    def test_create_table_duplicate(self, dynamodb_client, unique_name):
        dup_table = unique_name("DupTable")
        dynamodb_client.create_table(
            TableName=dup_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            with pytest.raises(ClientError) as exc:
                dynamodb_client.create_table(
                    TableName=dup_table,
                    AttributeDefinitions=[
                        {"AttributeName": "id", "AttributeType": "S"}
                    ],
                    KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
                    BillingMode="PAY_PER_REQUEST",
                )
            assert_client_error(exc, "ResourceInUseException")
        finally:
            try:
                dynamodb_client.delete_table(TableName=dup_table)
            except Exception:
                pass


class TestReturnConsumedCapacity:
    def test_query_return_consumed_capacity(self, dynamodb_client, unique_name):
        q_table = unique_name("QCapTable")
        dynamodb_client.create_table(
            TableName=q_table,
            AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb_client.put_item(TableName=q_table, Item={"pk": {"S": "key1"}})
            resp = dynamodb_client.query(
                TableName=q_table,
                KeyConditionExpression="pk = :pk",
                ExpressionAttributeValues={":pk": {"S": "key1"}},
                ReturnConsumedCapacity="TOTAL",
            )
            assert resp.get("ConsumedCapacity")
            assert resp["ConsumedCapacity"]["TableName"] == q_table
        finally:
            try:
                dynamodb_client.delete_table(TableName=q_table)
            except Exception:
                pass


class TestReturnValues:
    def test_put_item_return_values(self, dynamodb_client, unique_name):
        rv_table = unique_name("RVTable")
        dynamodb_client.create_table(
            TableName=rv_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            resp1 = dynamodb_client.put_item(
                TableName=rv_table,
                Item={"id": {"S": "rv1"}, "name": {"S": "Alice"}, "count": {"N": "10"}},
                ReturnValues="ALL_OLD",
            )
            assert resp1.get("Attributes") is None
            resp2 = dynamodb_client.put_item(
                TableName=rv_table,
                Item={"id": {"S": "rv1"}, "name": {"S": "Bob"}, "count": {"N": "20"}},
                ReturnValues="ALL_OLD",
            )
            assert resp2.get("Attributes") is not None
            assert resp2["Attributes"]["name"]["S"] == "Alice"
        finally:
            try:
                dynamodb_client.delete_table(TableName=rv_table)
            except Exception:
                pass

    def test_update_item_return_updated_attributes(self, dynamodb_client, unique_name):
        ua_table = unique_name("UATable")
        dynamodb_client.create_table(
            TableName=ua_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb_client.put_item(
                TableName=ua_table,
                Item={
                    "id": {"S": "ua1"},
                    "val": {"N": "0"},
                    "tags": {"L": [{"S": "a"}]},
                },
            )
            resp = dynamodb_client.update_item(
                TableName=ua_table,
                Key={"id": {"S": "ua1"}},
                UpdateExpression="ADD #v :inc SET #t = list_append(#t, :newTag)",
                ExpressionAttributeNames={"#v": "val", "#t": "tags"},
                ExpressionAttributeValues={
                    ":inc": {"N": "5"},
                    ":newTag": {"L": [{"S": "b"}]},
                },
                ReturnValues="ALL_NEW",
            )
            assert resp.get("Attributes")
            assert resp["Attributes"]["val"]["N"] == "5"
            assert len(resp["Attributes"]["tags"]["L"]) == 2
        finally:
            try:
                dynamodb_client.delete_table(TableName=ua_table)
            except Exception:
                pass
