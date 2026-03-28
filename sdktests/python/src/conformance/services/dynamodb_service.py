import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_dynamodb_tests(
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
    dynamodb = session.client("dynamodb", endpoint_url=endpoint, region_name=region)

    table_name = _make_unique_name("PyTable")
    table_arn = f"arn:aws:dynamodb:{region}:000000000000:table/{table_name}"

    def _create_table():
        dynamodb.create_table(
            TableName=table_name,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )

    results.append(await runner.run_test("dynamodb", "CreateTable", _create_table))

    def _describe_table():
        resp = dynamodb.describe_table(TableName=table_name)
        assert resp.get("Table"), "Table is null"

    results.append(await runner.run_test("dynamodb", "DescribeTable", _describe_table))

    def _list_tables():
        resp = dynamodb.list_tables()
        assert resp.get("TableNames") is not None

    results.append(await runner.run_test("dynamodb", "ListTables", _list_tables))

    def _put_item():
        dynamodb.put_item(
            TableName=table_name,
            Item={
                "id": {"S": "test1"},
                "name": {"S": "Test Item"},
                "count": {"N": "42"},
            },
        )

    results.append(await runner.run_test("dynamodb", "PutItem", _put_item))

    def _get_item():
        resp = dynamodb.get_item(TableName=table_name, Key={"id": {"S": "test1"}})
        assert resp.get("Item"), "Item is null"

    results.append(await runner.run_test("dynamodb", "GetItem", _get_item))

    def _update_item():
        resp = dynamodb.update_item(
            TableName=table_name,
            Key={"id": {"S": "test1"}},
            UpdateExpression="SET #n = :name",
            ExpressionAttributeNames={"#n": "name"},
            ExpressionAttributeValues={":name": {"S": "Updated"}},
            ReturnValues="ALL_NEW",
        )
        assert resp.get("Attributes"), "attributes not found"

    results.append(await runner.run_test("dynamodb", "UpdateItem", _update_item))

    def _query():
        resp = dynamodb.query(
            TableName=table_name,
            KeyConditionExpression="id = :id",
            ExpressionAttributeValues={":id": {"S": "test1"}},
        )
        assert resp.get("Count", 0) > 0

    results.append(await runner.run_test("dynamodb", "Query", _query))

    def _scan():
        resp = dynamodb.scan(TableName=table_name)
        assert resp.get("Count", 0) > 0

    results.append(await runner.run_test("dynamodb", "Scan", _scan))

    def _batch_write():
        resp = dynamodb.batch_write_item(
            RequestItems={
                table_name: [
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

    results.append(await runner.run_test("dynamodb", "BatchWriteItem", _batch_write))

    def _batch_get():
        resp = dynamodb.batch_get_item(
            RequestItems={
                table_name: {"Keys": [{"id": {"S": "batch1"}}, {"id": {"S": "batch2"}}]}
            }
        )
        table_resp = resp.get("Responses", {}).get(table_name)
        assert table_resp and len(table_resp) > 0

    results.append(await runner.run_test("dynamodb", "BatchGetItem", _batch_get))

    def _delete_item():
        dynamodb.delete_item(TableName=table_name, Key={"id": {"S": "test1"}})

    results.append(await runner.run_test("dynamodb", "DeleteItem", _delete_item))

    def _tag_resource():
        dynamodb.tag_resource(
            ResourceArn=table_arn,
            Tags=[{"Key": "Environment", "Value": "Test"}],
        )

    results.append(await runner.run_test("dynamodb", "TagResource", _tag_resource))

    def _list_tags_of_resource():
        resp = dynamodb.list_tags_of_resource(ResourceArn=table_arn)
        assert len(resp.get("Tags", [])) > 0, "no tags found"

    results.append(
        await runner.run_test("dynamodb", "ListTagsOfResource", _list_tags_of_resource)
    )

    def _untag_resource():
        dynamodb.untag_resource(ResourceArn=table_arn, TagKeys=["Environment"])

    results.append(await runner.run_test("dynamodb", "UntagResource", _untag_resource))

    def _update_time_to_live():
        resp = dynamodb.update_time_to_live(
            TableName=table_name,
            TimeToLiveSpecification={"AttributeName": "ttl", "Enabled": True},
        )
        assert resp.get("TimeToLiveSpecification"), "TimeToLiveSpecification is nil"

    results.append(
        await runner.run_test("dynamodb", "UpdateTimeToLive", _update_time_to_live)
    )

    def _describe_time_to_live():
        resp = dynamodb.describe_time_to_live(TableName=table_name)
        assert resp.get("TimeToLiveDescription"), "TTL description not found"

    results.append(
        await runner.run_test("dynamodb", "DescribeTimeToLive", _describe_time_to_live)
    )

    def _create_backup():
        backup_name = _make_unique_name("PyBackup")
        resp = dynamodb.create_backup(TableName=table_name, BackupName=backup_name)
        assert resp.get("BackupDetails"), "BackupDetails is nil"

    results.append(await runner.run_test("dynamodb", "CreateBackup", _create_backup))

    def _list_backups():
        resp = dynamodb.list_backups()
        assert resp.get("BackupSummaries") is not None

    results.append(await runner.run_test("dynamodb", "ListBackups", _list_backups))

    def _describe_continuous_backups():
        resp = dynamodb.describe_continuous_backups(TableName=table_name)
        assert resp.get("ContinuousBackupsDescription"), (
            "continuous backups description not found"
        )

    results.append(
        await runner.run_test(
            "dynamodb", "DescribeContinuousBackups", _describe_continuous_backups
        )
    )

    def _update_continuous_backups():
        resp = dynamodb.update_continuous_backups(
            TableName=table_name,
            PointInTimeRecoverySpecification={"PointInTimeRecoveryEnabled": True},
        )
        assert resp.get("ContinuousBackupsDescription"), (
            "ContinuousBackupsDescription is nil"
        )

    results.append(
        await runner.run_test(
            "dynamodb", "UpdateContinuousBackups", _update_continuous_backups
        )
    )

    def _execute_statement_insert():
        dynamodb.execute_statement(
            Statement=f"INSERT INTO \"{table_name}\" VALUE {{'id': 'partiql1', 'name': 'PartiQL Item'}}"
        )

    results.append(
        await runner.run_test(
            "dynamodb", "ExecuteStatement (PartiQL)", _execute_statement_insert
        )
    )

    def _execute_statement_select():
        resp = dynamodb.execute_statement(
            Statement=f"SELECT * FROM \"{table_name}\" WHERE id = 'partiql1'"
        )
        assert len(resp.get("Items", [])) > 0, "no items found"

    results.append(
        await runner.run_test(
            "dynamodb", "ExecuteStatement (SELECT)", _execute_statement_select
        )
    )

    def _transact_write_items():
        dynamodb.transact_write_items(
            TransactItems=[
                {
                    "Put": {
                        "TableName": table_name,
                        "Item": {
                            "id": {"S": "transact1"},
                            "name": {"S": "Transact Item"},
                        },
                    }
                }
            ]
        )

    results.append(
        await runner.run_test("dynamodb", "TransactWriteItems", _transact_write_items)
    )

    def _transact_get_items():
        resp = dynamodb.transact_get_items(
            TransactItems=[
                {
                    "Get": {
                        "TableName": table_name,
                        "Key": {"id": {"S": "transact1"}},
                    }
                }
            ]
        )
        assert len(resp.get("Responses", [])) > 0, "no responses"

    results.append(
        await runner.run_test("dynamodb", "TransactGetItems", _transact_get_items)
    )

    def _batch_execute_statement():
        resp = dynamodb.batch_execute_statement(
            Statements=[
                {
                    "Statement": f"UPDATE \"{table_name}\" SET #n = :name WHERE id = 'batch1'",
                    "Parameters": [{"S": "Updated via Batch"}],
                }
            ]
        )
        assert resp.get("Responses") is not None

    results.append(
        await runner.run_test(
            "dynamodb", "BatchExecuteStatement", _batch_execute_statement
        )
    )

    def _execute_transaction():
        resp = dynamodb.execute_transaction(
            TransactStatements=[
                {"Statement": f"SELECT * FROM \"{table_name}\" WHERE id = 'transact1'"}
            ]
        )
        assert len(resp.get("Responses", [])) > 0, "no responses"

    results.append(
        await runner.run_test("dynamodb", "ExecuteTransaction", _execute_transaction)
    )

    def _update_table():
        resp = dynamodb.update_table(TableName=table_name)
        assert resp.get("TableDescription"), "TableDescription is nil"

    results.append(await runner.run_test("dynamodb", "UpdateTable", _update_table))

    def _delete_table():
        dynamodb.delete_table(TableName=table_name)

    results.append(await runner.run_test("dynamodb", "DeleteTable", _delete_table))

    def _get_nonexistent_table():
        try:
            dynamodb.get_item(TableName="NoSuchTable_xyz", Key={"id": {"S": "test1"}})
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "GetItem_NonExistentTable", _get_nonexistent_table
        )
    )

    def _put_nonexistent_table():
        try:
            dynamodb.put_item(TableName="NoSuchTable_xyz", Item={"id": {"S": "k"}})
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "PutItem_NonExistentTable", _put_nonexistent_table
        )
    )

    def _query_nonexistent_table():
        try:
            dynamodb.query(
                TableName="NoSuchTable_xyz",
                KeyConditionExpression="id = :id",
                ExpressionAttributeValues={":id": {"S": "k"}},
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "Query_NonExistentTable", _query_nonexistent_table
        )
    )

    def _scan_nonexistent_table():
        try:
            dynamodb.scan(TableName="NoSuchTable_xyz")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "Scan_NonExistentTable", _scan_nonexistent_table
        )
    )

    def _describe_nonexistent_table():
        try:
            dynamodb.describe_table(TableName="NoSuchTable_xyz")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "DescribeTable_NonExistentTable", _describe_nonexistent_table
        )
    )

    def _delete_nonexistent_table():
        try:
            dynamodb.delete_table(TableName="NoSuchTable_xyz")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "DeleteTable_NonExistentTable", _delete_nonexistent_table
        )
    )

    def _update_item_conditional_check_fail():
        err_table = _make_unique_name("CondTable")
        dynamodb.create_table(
            TableName=err_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb.put_item(
                TableName=err_table,
                Item={"id": {"S": "cond1"}, "status": {"S": "active"}},
            )
            try:
                dynamodb.update_item(
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
                raise AssertionError("expected ConditionalCheckFailedException")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "ConditionalCheckFailedException"
        finally:
            try:
                dynamodb.delete_table(TableName=err_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb",
            "UpdateItem_ConditionalCheckFail",
            _update_item_conditional_check_fail,
        )
    )

    def _get_item_nonexistent_key():
        err_table = _make_unique_name("GetItemErr")
        dynamodb.create_table(
            TableName=err_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            resp = dynamodb.get_item(
                TableName=err_table, Key={"id": {"S": "nonexistent"}}
            )
            assert len(resp.get("Item", {})) == 0, (
                "expected empty item for non-existent key"
            )
        finally:
            try:
                dynamodb.delete_table(TableName=err_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb", "GetItem_NonExistentKey", _get_item_nonexistent_key
        )
    )

    def _delete_item_conditional_check_fail():
        err_table = _make_unique_name("DelCondTable")
        dynamodb.create_table(
            TableName=err_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb.put_item(
                TableName=err_table,
                Item={"id": {"S": "del1"}, "status": {"S": "active"}},
            )
            try:
                dynamodb.delete_item(
                    TableName=err_table,
                    Key={"id": {"S": "del1"}},
                    ConditionExpression="attribute_not_exists(id)",
                )
                raise AssertionError("expected ConditionalCheckFailedException")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "ConditionalCheckFailedException"
        finally:
            try:
                dynamodb.delete_table(TableName=err_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb",
            "DeleteItem_ConditionalCheckFail",
            _delete_item_conditional_check_fail,
        )
    )

    def _batch_get_nonexistent_table():
        try:
            dynamodb.batch_get_item(
                RequestItems={"NonExistentTable_xyz": {"Keys": [{"id": {"S": "k"}}]}}
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "dynamodb", "BatchGetItem_NonExistentTable", _batch_get_nonexistent_table
        )
    )

    def _create_table_duplicate():
        dup_table = _make_unique_name("DupTable")
        dynamodb.create_table(
            TableName=dup_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            try:
                dynamodb.create_table(
                    TableName=dup_table,
                    AttributeDefinitions=[
                        {"AttributeName": "id", "AttributeType": "S"}
                    ],
                    KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
                    BillingMode="PAY_PER_REQUEST",
                )
                raise AssertionError("expected error for duplicate table name")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "ResourceInUseException"
        finally:
            try:
                dynamodb.delete_table(TableName=dup_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb", "CreateTable_DuplicateName", _create_table_duplicate
        )
    )

    def _query_return_consumed_capacity():
        q_table = _make_unique_name("QCapTable")
        dynamodb.create_table(
            TableName=q_table,
            AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb.put_item(TableName=q_table, Item={"pk": {"S": "key1"}})
            resp = dynamodb.query(
                TableName=q_table,
                KeyConditionExpression="pk = :pk",
                ExpressionAttributeValues={":pk": {"S": "key1"}},
                ReturnConsumedCapacity="TOTAL",
            )
            assert resp.get("ConsumedCapacity"), "expected ConsumedCapacity in response"
            assert resp["ConsumedCapacity"]["TableName"] == q_table
        finally:
            try:
                dynamodb.delete_table(TableName=q_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb", "Query_ReturnConsumedCapacity", _query_return_consumed_capacity
        )
    )

    def _put_item_return_values():
        rv_table = _make_unique_name("RVTable")
        dynamodb.create_table(
            TableName=rv_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            resp1 = dynamodb.put_item(
                TableName=rv_table,
                Item={"id": {"S": "rv1"}, "name": {"S": "Alice"}, "count": {"N": "10"}},
                ReturnValues="ALL_OLD",
            )
            assert resp1.get("Attributes") is None, (
                "first PutItem with ReturnValues=ALL_OLD should have nil Attributes"
            )
            resp2 = dynamodb.put_item(
                TableName=rv_table,
                Item={"id": {"S": "rv1"}, "name": {"S": "Bob"}, "count": {"N": "20"}},
                ReturnValues="ALL_OLD",
            )
            assert resp2.get("Attributes") is not None, (
                "second PutItem with ReturnValues=ALL_OLD should return old attributes"
            )
            assert resp2["Attributes"]["name"]["S"] == "Alice"
        finally:
            try:
                dynamodb.delete_table(TableName=rv_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb", "PutItem_ReturnValues", _put_item_return_values
        )
    )

    def _update_item_return_updated_attributes():
        ua_table = _make_unique_name("UATable")
        dynamodb.create_table(
            TableName=ua_table,
            AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "S"}],
            KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
            BillingMode="PAY_PER_REQUEST",
        )
        try:
            dynamodb.put_item(
                TableName=ua_table,
                Item={
                    "id": {"S": "ua1"},
                    "val": {"N": "0"},
                    "tags": {"L": [{"S": "a"}]},
                },
            )
            resp = dynamodb.update_item(
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
            assert resp.get("Attributes"), "expected updated attributes"
            assert resp["Attributes"]["val"]["N"] == "5"
            assert len(resp["Attributes"]["tags"]["L"]) == 2
        finally:
            try:
                dynamodb.delete_table(TableName=ua_table)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "dynamodb",
            "UpdateItem_ReturnUpdatedAttributes",
            _update_item_return_updated_attributes,
        )
    )

    return results
