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


class TestPutItem:
    def test_put_item(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={
                "id": {"S": "test1"},
                "name": {"S": "Test Item"},
                "count": {"N": "42"},
            },
        )


class TestGetItem:
    def test_get_item(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={"id": {"S": "get1"}, "name": {"S": "Get Item"}},
        )
        resp = dynamodb_client.get_item(
            TableName=dynamodb_table["name"], Key={"id": {"S": "get1"}}
        )
        assert resp.get("Item")


class TestUpdateItem:
    def test_update_item(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={"id": {"S": "upd1"}, "name": {"S": "Original"}},
        )
        resp = dynamodb_client.update_item(
            TableName=dynamodb_table["name"],
            Key={"id": {"S": "upd1"}},
            UpdateExpression="SET #n = :name",
            ExpressionAttributeNames={"#n": "name"},
            ExpressionAttributeValues={":name": {"S": "Updated"}},
            ReturnValues="ALL_NEW",
        )
        assert resp.get("Attributes")


class TestDeleteItem:
    def test_delete_item(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={"id": {"S": "del1"}, "name": {"S": "To Delete"}},
        )
        dynamodb_client.delete_item(
            TableName=dynamodb_table["name"], Key={"id": {"S": "del1"}}
        )


class TestQuery:
    def test_query(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={"id": {"S": "query1"}, "name": {"S": "Query Item"}},
        )
        resp = dynamodb_client.query(
            TableName=dynamodb_table["name"],
            KeyConditionExpression="id = :id",
            ExpressionAttributeValues={":id": {"S": "query1"}},
        )
        assert resp.get("Count", 0) > 0


class TestScan:
    def test_scan(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={"id": {"S": "scan1"}, "name": {"S": "Scan Item"}},
        )
        resp = dynamodb_client.scan(TableName=dynamodb_table["name"])
        assert resp.get("Count", 0) > 0


class TestBatchWriteItem:
    def test_batch_write_item(self, dynamodb_client, dynamodb_table):
        resp = dynamodb_client.batch_write_item(
            RequestItems={
                dynamodb_table["name"]: [
                    {
                        "PutRequest": {
                            "Item": {
                                "id": {"S": "batch1"},
                                "data": {"S": "batch item 1"},
                            }
                        }
                    },
                    {
                        "PutRequest": {
                            "Item": {
                                "id": {"S": "batch2"},
                                "data": {"S": "batch item 2"},
                            }
                        }
                    },
                ],
            }
        )
        assert resp.get("UnprocessedItems") is not None


class TestBatchGetItem:
    def test_batch_get_item(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table["name"],
            Item={"id": {"S": "bg1"}, "data": {"S": "batch get item"}},
        )
        resp = dynamodb_client.batch_get_item(
            RequestItems={dynamodb_table["name"]: {"Keys": [{"id": {"S": "bg1"}}]}}
        )
        table_resp = resp.get("Responses", {}).get(dynamodb_table["name"])
        assert table_resp and len(table_resp) > 0
