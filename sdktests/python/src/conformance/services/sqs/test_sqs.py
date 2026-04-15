import json
import time

import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="module")
def queue_url(sqs_client, unique_name):
    name = unique_name("PyQueue")
    resp = sqs_client.create_queue(
        QueueName=name,
        Attributes={
            "DelaySeconds": "0",
            "MaximumMessageSize": "262144",
            "VisibilityTimeout": "30",
            "ReceiveMessageWaitTimeSeconds": "0",
        },
    )
    url = resp["QueueUrl"]
    yield {"url": url, "name": name}
    try:
        sqs_client.delete_queue(QueueUrl=url)
    except Exception:
        pass


class TestQueueLifecycle:
    def test_create_queue(self, queue_url):
        assert queue_url["url"]

    def test_get_queue_url(self, sqs_client, queue_url):
        resp = sqs_client.get_queue_url(QueueName=queue_url["name"])
        assert resp.get("QueueUrl")

    def test_get_queue_attributes(self, sqs_client, queue_url):
        resp = sqs_client.get_queue_attributes(
            QueueUrl=queue_url["url"],
            AttributeNames=["All"],
        )
        assert resp.get("Attributes")
        assert resp["Attributes"].get("QueueArn")

    def test_set_queue_attributes(self, sqs_client, queue_url):
        sqs_client.set_queue_attributes(
            QueueUrl=queue_url["url"],
            Attributes={"VisibilityTimeout": "45"},
        )
        resp = sqs_client.get_queue_attributes(
            QueueUrl=queue_url["url"],
            AttributeNames=["VisibilityTimeout"],
        )
        assert resp["Attributes"]["VisibilityTimeout"] == "45"

    def test_list_queues(self, sqs_client):
        resp = sqs_client.list_queues()
        assert resp.get("QueueUrls") is not None


class TestMessaging:
    def test_send_message(self, sqs_client, queue_url):
        resp = sqs_client.send_message(
            QueueUrl=queue_url["url"],
            MessageBody=json.dumps({"test": "hello", "timestamp": int(time.time())}),
            MessageAttributes={
                "AttributeName": {
                    "DataType": "String",
                    "StringValue": "AttributeValue",
                }
            },
        )
        assert resp.get("MessageId")

    def test_send_message_batch(self, sqs_client, queue_url):
        resp = sqs_client.send_message_batch(
            QueueUrl=queue_url["url"],
            Entries=[
                {"Id": "msg-1", "MessageBody": "Batch message 1"},
                {"Id": "msg-2", "MessageBody": "Batch message 2"},
                {"Id": "msg-3", "MessageBody": "Batch message 3"},
            ],
        )
        assert len(resp.get("Successful", [])) == 3

    def test_receive_message(self, sqs_client, queue_url):
        resp = sqs_client.receive_message(
            QueueUrl=queue_url["url"],
            MaxNumberOfMessages=10,
            WaitTimeSeconds=1,
        )
        assert resp.get("Messages") is not None
        assert len(resp["Messages"]) > 0

    def test_receive_message_empty(self, sqs_client, queue_url):
        resp = sqs_client.receive_message(
            QueueUrl=queue_url["url"],
            MaxNumberOfMessages=1,
            WaitTimeSeconds=1,
        )
        assert resp.get("Messages", []) == []

    def test_delete_message(self, sqs_client, queue_url):
        receive_resp = sqs_client.receive_message(
            QueueUrl=queue_url["url"],
            MaxNumberOfMessages=1,
        )
        if receive_resp.get("Messages"):
            receipt_handle = receive_resp["Messages"][0]["ReceiptHandle"]
            assert receipt_handle
            sqs_client.delete_message(
                QueueUrl=queue_url["url"],
                ReceiptHandle=receipt_handle,
            )

    def test_delete_message_batch(self, sqs_client, queue_url):
        send_resp = sqs_client.send_message_batch(
            QueueUrl=queue_url["url"],
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
        sqs_client.delete_message_batch(QueueUrl=queue_url["url"], Entries=entries)


class TestQueueTags:
    def test_tag_queue(self, sqs_client, queue_url):
        sqs_client.tag_queue(
            QueueUrl=queue_url["url"],
            Tags={"Environment": "Test", "Team": "Platform"},
        )

    def test_list_queue_tags(self, sqs_client, queue_url):
        resp = sqs_client.list_queue_tags(QueueUrl=queue_url["url"])
        assert resp.get("Tags")
        assert resp["Tags"].get("Environment") == "Test"

    def test_untag_queue(self, sqs_client, queue_url):
        sqs_client.untag_queue(QueueUrl=queue_url["url"], TagKeys=["Environment"])
        resp = sqs_client.list_queue_tags(QueueUrl=queue_url["url"])
        if resp.get("Tags") and resp["Tags"].get("Environment"):
            raise AssertionError("Environment tag should be removed")


class TestQueuePermissions:
    def test_add_permission(self, sqs_client, queue_url):
        sqs_client.add_permission(
            QueueUrl=queue_url["url"],
            Label="AllowS3Access",
            AWSAccountIds=["000000000000"],
            Actions=["sqs:ReceiveMessage", "sqs:SendMessage"],
        )

    def test_remove_permission(self, sqs_client, queue_url):
        sqs_client.remove_permission(QueueUrl=queue_url["url"], Label="AllowS3Access")


class TestPurge:
    def test_purge_queue(self, sqs_client, queue_url):
        sqs_client.purge_queue(QueueUrl=queue_url["url"])


class TestErrorCases:
    def test_create_queue_idempotent(self, sqs_client, unique_name):
        name = unique_name("DupQ")
        resp1 = sqs_client.create_queue(QueueName=name)
        resp2 = sqs_client.create_queue(QueueName=name)
        assert resp1["QueueUrl"] == resp2["QueueUrl"]

    def test_create_queue_duplicate_different_attrs(self, sqs_client, unique_name):
        name = unique_name("DupQAttrs")
        sqs_client.create_queue(QueueName=name, Attributes={"VisibilityTimeout": "30"})
        with pytest.raises(ClientError) as exc_info:
            sqs_client.create_queue(
                QueueName=name, Attributes={"VisibilityTimeout": "99"}
            )
        assert_client_error(exc_info, "QueueNameExists")

    def test_get_queue_url_nonexistent(self, sqs_client):
        with pytest.raises(ClientError) as exc_info:
            sqs_client.get_queue_url(QueueName="NonExistentQueue_xyz_12345")
        assert_client_error(
            exc_info, ("AWS.SimpleQueueService.NonExistentQueue", "QueueDoesNotExist")
        )

    def test_send_message_invalid_queue(self, sqs_client):
        with pytest.raises(ClientError):
            sqs_client.send_message(
                QueueUrl="https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent",
                MessageBody="test",
            )

    def test_receive_message_invalid_queue(self, sqs_client):
        with pytest.raises(ClientError):
            sqs_client.receive_message(
                QueueUrl="https://invalid-queue-url-xyz12345.sqs.region.amazonaws.com/000000000000/NonExistent",
                MaxNumberOfMessages=1,
            )


class TestMultiByte:
    def test_multi_byte_message(self, sqs_client, queue_url):
        ja_body = "日本語テストメッセージ"
        zh_body = "简体中文测试消息"
        tw_body = "繁體中文測試訊息"
        for body in [ja_body, zh_body, tw_body]:
            sqs_client.send_message(QueueUrl=queue_url["url"], MessageBody=body)
        received = set()
        for _ in range(3):
            resp = sqs_client.receive_message(
                QueueUrl=queue_url["url"], MaxNumberOfMessages=1, WaitTimeSeconds=2
            )
            for msg in resp.get("Messages", []):
                received.add(msg["Body"])
                sqs_client.delete_message(
                    QueueUrl=queue_url["url"], ReceiptHandle=msg["ReceiptHandle"]
                )
        assert ja_body in received
        assert zh_body in received
        assert tw_body in received


class TestDeleteQueue:
    def test_delete_queue(self, sqs_client, queue_url):
        sqs_client.delete_queue(QueueUrl=queue_url["url"])
