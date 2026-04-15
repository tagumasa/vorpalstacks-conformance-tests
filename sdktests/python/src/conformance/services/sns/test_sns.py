import json
import time

import pytest

from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="class")
def sns_topic(sns_client, unique_name):
    topic_name = unique_name("topic")
    resp = sns_client.create_topic(
        Name=topic_name,
        Attributes={
            "DisplayName": "Test Topic",
            "DeliveryPolicy": json.dumps(
                {
                    "defaultHealthyRetryPolicy": {
                        "minDelayTarget": 1,
                        "maxDelayTarget": 60,
                        "numRetries": 3,
                        "numNoDelayRetries": 0,
                    }
                }
            ),
        },
        Tags=[{"Key": "Environment", "Value": "Test"}],
    )
    topic_arn = resp["TopicArn"]
    yield topic_arn
    try:
        sns_client.delete_topic(TopicArn=topic_arn)
    except Exception:
        pass


class TestCreateTopic:
    def test_create_topic(self, sns_client, unique_name):
        topic_name = unique_name("topic")
        resp = sns_client.create_topic(Name=topic_name)
        assert resp.get("TopicArn"), "TopicArn is null"
        sns_client.delete_topic(TopicArn=resp["TopicArn"])

    def test_duplicate(self, sns_client, unique_name):
        topic_name = unique_name("topic")
        resp = sns_client.create_topic(Name=topic_name)
        assert resp.get("TopicArn"), (
            "TopicArn is null (CreateTopic should be idempotent)"
        )
        sns_client.delete_topic(TopicArn=resp["TopicArn"])


class TestGetTopicAttributes:
    def test_get_topic_attributes(self, sns_client, sns_topic):
        resp = sns_client.get_topic_attributes(TopicArn=sns_topic)
        assert resp.get("Attributes"), "Attributes is null"
        assert resp["Attributes"].get("TopicArn"), "TopicArn is missing"
        assert resp["Attributes"].get("DisplayName") == "Test Topic"

    def test_nonexistent(self, sns_client):
        with pytest.raises(ClientError) as exc_info:
            sns_client.get_topic_attributes(
                TopicArn="arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345"
            )
        assert_client_error(exc_info, "NotFound")


class TestSetTopicAttributes:
    def test_set_topic_attributes(self, sns_client, sns_topic):
        sns_client.set_topic_attributes(
            TopicArn=sns_topic,
            AttributeName="DisplayName",
            AttributeValue="Updated Topic",
        )
        resp = sns_client.get_topic_attributes(TopicArn=sns_topic)
        assert resp["Attributes"]["DisplayName"] == "Updated Topic"


class TestListTopics:
    def test_list_topics(self, sns_client):
        resp = sns_client.list_topics()
        assert resp.get("Topics") is not None


class TestSubscribe:
    def test_subscribe(self, sns_client, sns_topic):
        resp = sns_client.subscribe(
            TopicArn=sns_topic,
            Protocol="sqs",
            Endpoint="https://example.sqs.amazonaws.com/000000000000/test",
        )
        assert resp.get("SubscriptionArn"), "SubscriptionArn is null"

    def test_nonexistent(self, sns_client):
        with pytest.raises(ClientError) as exc_info:
            sns_client.subscribe(
                TopicArn="arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345",
                Protocol="sqs",
                Endpoint="https://example.sqs.amazonaws.com/000000000000/test",
            )
        assert_client_error(exc_info, "NotFound")


class TestListSubscriptions:
    def test_list_subscriptions(self, sns_client):
        resp = sns_client.list_subscriptions()
        assert resp.get("Subscriptions") is not None

    def test_list_subscriptions_by_topic(self, sns_client, sns_topic):
        resp = sns_client.list_subscriptions_by_topic(TopicArn=sns_topic)
        assert resp.get("Subscriptions") is not None


class TestPublish:
    def test_publish(self, sns_client, sns_topic):
        resp = sns_client.publish(
            TopicArn=sns_topic,
            Message=json.dumps({"test": "hello", "timestamp": int(time.time())}),
            Subject="Test Message",
            MessageAttributes={
                "AttributeName": {
                    "DataType": "String",
                    "StringValue": "AttributeValue",
                },
            },
        )
        assert resp.get("MessageId"), "MessageId is null"

    def test_target_arn(self, sns_client, sns_topic):
        resp = sns_client.publish(TopicArn=sns_topic, Message="Test message to target")
        assert resp.get("MessageId"), "MessageId is null"

    def test_batch(self, sns_client, sns_topic):
        resp = sns_client.publish_batch(
            TopicArn=sns_topic,
            PublishBatchRequestEntries=[
                {"Id": "msg-1", "Message": "Batch message 1"},
                {"Id": "msg-2", "Message": "Batch message 2"},
                {"Id": "msg-3", "Message": "Batch message 3"},
            ],
        )
        assert len(resp.get("Successful", [])) == 3

    def test_nonexistent(self, sns_client):
        with pytest.raises(ClientError) as exc_info:
            sns_client.publish(
                TopicArn="arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345",
                Message="test",
            )
        assert_client_error(exc_info, "NotFound")

    def test_multi_byte(self, sns_client, sns_topic):
        ja_msg = "日本語テストメッセージ"
        zh_msg = "简体中文测试消息"
        tw_msg = "繁體中文測試訊息"
        for msg in [ja_msg, zh_msg, tw_msg]:
            resp = sns_client.publish(TopicArn=sns_topic, Message=msg)
            assert resp.get("MessageId"), "MessageId is null"


class TestUnsubscribe:
    def test_unsubscribe(self, sns_client, sns_topic):
        resp = sns_client.subscribe(
            TopicArn=sns_topic,
            Protocol="sqs",
            Endpoint="https://example.sqs.amazonaws.com/000000000000/test",
        )
        subscription_arn = resp.get("SubscriptionArn", "")
        if subscription_arn:
            sns_client.unsubscribe(SubscriptionArn=subscription_arn)


class TestDeleteTopic:
    def test_delete_topic(self, sns_client, unique_name):
        topic_name = unique_name("topic")
        resp = sns_client.create_topic(Name=topic_name)
        sns_client.delete_topic(TopicArn=resp["TopicArn"])


class TestTags:
    def test_tag_resource(self, sns_client, sns_topic):
        sns_client.tag_resource(
            ResourceArn=sns_topic, Tags=[{"Key": "Team", "Value": "Platform"}]
        )

    def test_list_tags_for_resource(self, sns_client, sns_topic):
        resp = sns_client.list_tags_for_resource(ResourceArn=sns_topic)
        assert resp.get("Tags") is not None
        tags_dict = {t["Key"]: t["Value"] for t in resp["Tags"]}
        assert tags_dict.get("Team") == "Platform"

    def test_untag_resource(self, sns_client, sns_topic):
        sns_client.untag_resource(ResourceArn=sns_topic, TagKeys=["Team"])
