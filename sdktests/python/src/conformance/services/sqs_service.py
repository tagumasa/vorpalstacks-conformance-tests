import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_sqs_tests(
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
    sqs_client = session.client("sqs", endpoint_url=endpoint, region_name=region)

    queue_name = _make_unique_name("PyQueue")
    queue_url = ""

    try:

        def _create_queue():
            nonlocal queue_url
            resp = sqs_client.create_queue(
                QueueName=queue_name,
                Attributes={
                    "DelaySeconds": "0",
                    "MaximumMessageSize": "262144",
                    "VisibilityTimeout": "30",
                    "ReceiveMessageWaitTimeSeconds": "0",
                },
            )
            if not resp.get("QueueUrl"):
                raise AssertionError("QueueUrl is null")
            queue_url = resp["QueueUrl"]

        results.append(await runner.run_test("sqs", "CreateQueue", _create_queue))

        def _get_queue_url():
            resp = sqs_client.get_queue_url(QueueName=queue_name)
            assert resp.get("QueueUrl"), "QueueUrl is null"

        results.append(await runner.run_test("sqs", "GetQueueUrl", _get_queue_url))

        def _get_queue_attributes():
            resp = sqs_client.get_queue_attributes(
                QueueUrl=queue_url,
                AttributeNames=["All"],
            )
            assert resp.get("Attributes"), "Attributes is null"
            assert resp["Attributes"].get("QueueArn"), "QueueArn is missing"

        results.append(
            await runner.run_test("sqs", "GetQueueAttributes", _get_queue_attributes)
        )

        def _set_queue_attributes():
            sqs_client.set_queue_attributes(
                QueueUrl=queue_url,
                Attributes={"VisibilityTimeout": "45"},
            )
            resp = sqs_client.get_queue_attributes(
                QueueUrl=queue_url,
                AttributeNames=["VisibilityTimeout"],
            )
            assert resp["Attributes"]["VisibilityTimeout"] == "45"

        results.append(
            await runner.run_test("sqs", "SetQueueAttributes", _set_queue_attributes)
        )

        def _list_queues():
            resp = sqs_client.list_queues()
            assert resp.get("QueueUrls") is not None

        results.append(await runner.run_test("sqs", "ListQueues", _list_queues))

        def _send_message():
            resp = sqs_client.send_message(
                QueueUrl=queue_url,
                MessageBody=json.dumps(
                    {"test": "hello", "timestamp": int(time.time())}
                ),
                MessageAttributes={
                    "AttributeName": {
                        "DataType": "String",
                        "StringValue": "AttributeValue",
                    }
                },
            )
            assert resp.get("MessageId"), "MessageId is null"

        results.append(await runner.run_test("sqs", "SendMessage", _send_message))

        def _send_message_batch():
            resp = sqs_client.send_message_batch(
                QueueUrl=queue_url,
                Entries=[
                    {"Id": "msg-1", "MessageBody": "Batch message 1"},
                    {"Id": "msg-2", "MessageBody": "Batch message 2"},
                    {"Id": "msg-3", "MessageBody": "Batch message 3"},
                ],
            )
            assert len(resp.get("Successful", [])) == 3

        results.append(
            await runner.run_test("sqs", "SendMessageBatch", _send_message_batch)
        )

        def _receive_message():
            resp = sqs_client.receive_message(
                QueueUrl=queue_url,
                MaxNumberOfMessages=10,
                WaitTimeSeconds=1,
            )
            assert resp.get("Messages") is not None
            assert len(resp["Messages"]) > 0

        results.append(await runner.run_test("sqs", "ReceiveMessage", _receive_message))

        def _receive_message_empty():
            resp = sqs_client.receive_message(
                QueueUrl=queue_url,
                MaxNumberOfMessages=1,
                WaitTimeSeconds=1,
            )
            assert resp.get("Messages", []) == [], "Expected no messages"

        results.append(
            await runner.run_test("sqs", "ReceiveMessage_Empty", _receive_message_empty)
        )

        def _delete_message():
            receive_resp = sqs_client.receive_message(
                QueueUrl=queue_url,
                MaxNumberOfMessages=1,
            )
            if receive_resp.get("Messages"):
                receipt_handle = receive_resp["Messages"][0]["ReceiptHandle"]
                assert receipt_handle, "ReceiptHandle is null"
                sqs_client.delete_message(
                    QueueUrl=queue_url,
                    ReceiptHandle=receipt_handle,
                )

        results.append(await runner.run_test("sqs", "DeleteMessage", _delete_message))

        def _delete_message_batch():
            send_resp = sqs_client.send_message_batch(
                QueueUrl=queue_url,
                Entries=[
                    {"Id": "del-1", "MessageBody": "Delete me 1"},
                    {"Id": "del-2", "MessageBody": "Delete me 2"},
                ],
            )
            assert len(send_resp.get("Successful", [])) == 2
            entries = [
                {"Id": entry["Id"], "ReceiptHandle": "placeholder"}
                for entry in send_resp["Successful"]
            ]
            sqs_client.delete_message_batch(QueueUrl=queue_url, Entries=entries)

        results.append(
            await runner.run_test("sqs", "DeleteMessageBatch", _delete_message_batch)
        )

        def _tag_queue():
            sqs_client.tag_queue(
                QueueUrl=queue_url,
                Tags={"Environment": "Test", "Team": "Platform"},
            )

        results.append(await runner.run_test("sqs", "TagQueue", _tag_queue))

        def _list_queue_tags():
            resp = sqs_client.list_queue_tags(QueueUrl=queue_url)
            assert resp.get("Tags"), "Tags is null"
            assert resp["Tags"].get("Environment") == "Test"

        results.append(await runner.run_test("sqs", "ListQueueTags", _list_queue_tags))

        def _untag_queue():
            sqs_client.untag_queue(QueueUrl=queue_url, TagKeys=["Environment"])
            resp = sqs_client.list_queue_tags(QueueUrl=queue_url)
            if resp.get("Tags") and resp["Tags"].get("Environment"):
                raise AssertionError("Environment tag should be removed")

        results.append(await runner.run_test("sqs", "UntagQueue", _untag_queue))

        def _purge_queue():
            sqs_client.purge_queue(QueueUrl=queue_url)

        results.append(await runner.run_test("sqs", "PurgeQueue", _purge_queue))

        def _add_permission():
            sqs_client.add_permission(
                QueueUrl=queue_url,
                Label="AllowS3Access",
                AWSAccountIds=["000000000000"],
                Actions=["sqs:ReceiveMessage", "sqs:SendMessage"],
            )

        results.append(await runner.run_test("sqs", "AddPermission", _add_permission))

        def _remove_permission():
            sqs_client.remove_permission(QueueUrl=queue_url, Label="AllowS3Access")

        results.append(
            await runner.run_test("sqs", "RemovePermission", _remove_permission)
        )

        def _delete_queue():
            sqs_client.delete_queue(QueueUrl=queue_url)

        results.append(await runner.run_test("sqs", "DeleteQueue", _delete_queue))

    finally:
        try:
            if queue_url:
                sqs_client.delete_queue(QueueUrl=queue_url)
        except Exception:
            pass

    def _create_queue_duplicate():
        try:
            sqs_client.create_queue(QueueName=queue_name)
        except ClientError as e:
            assert e.response["Error"]["Code"] == "QueueNameExists"

    results.append(
        await runner.run_test("sqs", "CreateQueue_Duplicate", _create_queue_duplicate)
    )

    def _get_queue_url_nonexistent():
        try:
            sqs_client.get_queue_url(QueueName="NonExistentQueue_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] in (
                "AWS.SimpleQueueService.NonExistentQueue",
                "QueueDoesNotExist",
            )

    results.append(
        await runner.run_test(
            "sqs", "GetQueueUrl_NonExistent", _get_queue_url_nonexistent
        )
    )

    def _send_message_invalid_queue():
        try:
            sqs_client.send_message(
                QueueUrl="https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent",
                MessageBody="test",
            )
        except ClientError:
            pass

    results.append(
        await runner.run_test(
            "sqs", "SendMessage_InvalidQueue", _send_message_invalid_queue
        )
    )

    def _receive_message_invalid_queue():
        try:
            sqs_client.receive_message(
                QueueUrl="https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent",
                MaxNumberOfMessages=1,
            )
        except ClientError:
            pass

    results.append(
        await runner.run_test(
            "sqs", "ReceiveMessage_InvalidQueue", _receive_message_invalid_queue
        )
    )

    def _multi_byte_message():
        nonlocal queue_url
        ja_body = "日本語テストメッセージ"
        zh_body = "简体中文测试消息"
        tw_body = "繁體中文測試訊息"
        for body in [ja_body, zh_body, tw_body]:
            sqs_client.send_message(QueueUrl=queue_url, MessageBody=body)
        received = set()
        for _ in range(3):
            resp = sqs_client.receive_message(
                QueueUrl=queue_url, MaxNumberOfMessages=1, WaitTimeSeconds=2
            )
            for msg in resp.get("Messages", []):
                received.add(msg["Body"])
                sqs_client.delete_message(
                    QueueUrl=queue_url, ReceiptHandle=msg["ReceiptHandle"]
                )
        assert ja_body in received, f"Japanese message not received"
        assert zh_body in received, f"Simplified Chinese message not received"
        assert tw_body in received, f"Traditional Chinese message not received"

    results.append(
        await runner.run_test("sqs", "MultiByteMessage", _multi_byte_message)
    )

    return results
