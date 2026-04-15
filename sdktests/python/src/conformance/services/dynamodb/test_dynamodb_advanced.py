import pytest


@pytest.fixture(scope="module")
def dynamodb_table(dynamodb_client, region, unique_name):
    table_name = unique_name("PyAdvTable")
    dynamodb_client.create_table(
        TableName=table_name,
        AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
        KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
        BillingMode="PAY_PER_REQUEST",
    )
    yield table_name
    try:
        dynamodb_client.delete_table(TableName=table_name)
    except Exception:
        pass


class TestExecuteStatement:
    def test_execute_statement_insert(self, dynamodb_client, dynamodb_table):
        dynamodb_client.execute_statement(
            Statement=f"INSERT INTO \"{dynamodb_table}\" VALUE {{'id': 'partiql1', 'name': 'PartiQL Item'}}"
        )

    def test_execute_statement_select(self, dynamodb_client, dynamodb_table):
        dynamodb_client.execute_statement(
            Statement=f"INSERT INTO \"{dynamodb_table}\" VALUE {{'id': 'partiql-sel', 'name': 'PartiQL Select'}}"
        )
        resp = dynamodb_client.execute_statement(
            Statement=f"SELECT * FROM \"{dynamodb_table}\" WHERE id = 'partiql-sel'"
        )
        assert len(resp.get("Items", [])) > 0


class TestTransactWriteItems:
    def test_transact_write_items(self, dynamodb_client, dynamodb_table):
        dynamodb_client.transact_write_items(
            TransactItems=[
                {
                    "Put": {
                        "TableName": dynamodb_table,
                        "Item": {
                            "id": {"S": "transact1"},
                            "name": {"S": "Transact Item"},
                        },
                    }
                }
            ]
        )


class TestTransactGetItems:
    def test_transact_get_items(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table,
            Item={"id": {"S": "tget1"}, "name": {"S": "Transact Get Item"}},
        )
        resp = dynamodb_client.transact_get_items(
            TransactItems=[
                {
                    "Get": {
                        "TableName": dynamodb_table,
                        "Key": {"id": {"S": "tget1"}},
                    }
                }
            ]
        )
        assert len(resp.get("Responses", [])) > 0


class TestBatchExecuteStatement:
    def test_batch_execute_statement(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table,
            Item={"id": {"S": "bes1"}, "name": {"S": "Batch Exec"}},
        )
        resp = dynamodb_client.batch_execute_statement(
            Statements=[
                {
                    "Statement": f"UPDATE \"{dynamodb_table}\" SET #n = :name WHERE id = 'bes1'",
                    "Parameters": [{"S": "Updated via Batch"}],
                }
            ]
        )
        assert resp.get("Responses") is not None


class TestExecuteTransaction:
    def test_execute_transaction(self, dynamodb_client, dynamodb_table):
        dynamodb_client.put_item(
            TableName=dynamodb_table,
            Item={"id": {"S": "et1"}, "name": {"S": "Exec Transaction"}},
        )
        resp = dynamodb_client.execute_transaction(
            TransactStatements=[
                {"Statement": f"SELECT * FROM \"{dynamodb_table}\" WHERE id = 'et1'"}
            ]
        )
        assert len(resp.get("Responses", [])) > 0
