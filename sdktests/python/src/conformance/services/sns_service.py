import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_sns_tests(
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
    sns_client = session.client("sns", endpoint_url=endpoint, region_name=region)
    sqs_client = session.client("sqs", endpoint_url=endpoint, region_name=region)

    topic_name = _make_unique_name("PyTopic")
    sqs_queue_name = _make_unique_name("PySQSQueue")
    topic_arn = ""
    subscription_arn = ""
    queue_url = ""

    try:
        sqs_resp = sqs_client.create_queue(
            QueueName=sqs_queue_name,
            Attributes={"VisibilityTimeout": "30"},
        )
        queue_url = sqs_resp.get("QueueUrl", "")

        def _create_topic():
            nonlocal topic_arn
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
            assert resp.get("TopicArn"), "TopicArn is null"
            topic_arn = resp["TopicArn"]

        results.append(await runner.run_test("sns", "CreateTopic", _create_topic))

        def _get_topic_attributes():
            resp = sns_client.get_topic_attributes(TopicArn=topic_arn)
            assert resp.get("Attributes"), "Attributes is null"
            assert resp["Attributes"].get("TopicArn"), "TopicArn is missing"
            assert resp["Attributes"].get("DisplayName") == "Test Topic"

        results.append(
            await runner.run_test("sns", "GetTopicAttributes", _get_topic_attributes)
        )

        def _set_topic_attributes():
            sns_client.set_topic_attributes(
                TopicArn=topic_arn,
                AttributeName="DisplayName",
                AttributeValue="Updated Topic",
            )
            resp = sns_client.get_topic_attributes(TopicArn=topic_arn)
            assert resp["Attributes"]["DisplayName"] == "Updated Topic"

        results.append(
            await runner.run_test("sns", "SetTopicAttributes", _set_topic_attributes)
        )

        def _list_topics():
            resp = sns_client.list_topics()
            assert resp.get("Topics") is not None

        results.append(await runner.run_test("sns", "ListTopics", _list_topics))

        def _subscribe():
            nonlocal subscription_arn
            resp = sns_client.subscribe(
                TopicArn=topic_arn,
                Protocol="sqs",
                Endpoint=queue_url,
            )
            assert resp.get("SubscriptionArn"), "SubscriptionArn is null"
            subscription_arn = resp["SubscriptionArn"]

        results.append(await runner.run_test("sns", "Subscribe", _subscribe))

        def _list_subscriptions_by_topic():
            resp = sns_client.list_subscriptions_by_topic(TopicArn=topic_arn)
            assert resp.get("Subscriptions") is not None

        results.append(
            await runner.run_test(
                "sns", "ListSubscriptionsByTopic", _list_subscriptions_by_topic
            )
        )

        def _list_subscriptions():
            resp = sns_client.list_subscriptions()
            assert resp.get("Subscriptions") is not None

        results.append(
            await runner.run_test("sns", "ListSubscriptions", _list_subscriptions)
        )

        def _publish():
            resp = sns_client.publish(
                TopicArn=topic_arn,
                Message=json.dumps({"test": "hello", "timestamp": int(time.time())}),
                Subject="Test Message",
                MessageAttributes={
                    "AttributeName": {
                        "DataType": "String",
                        "StringValue": "AttributeValue",
                    }
                },
            )
            assert resp.get("MessageId"), "MessageId is null"

        results.append(await runner.run_test("sns", "Publish", _publish))

        def _publish_target_arn():
            resp = sns_client.publish(
                TopicArn=topic_arn,
                Message="Test message to target",
            )
            assert resp.get("MessageId"), "MessageId is null"

        results.append(
            await runner.run_test("sns", "Publish_TargetArn", _publish_target_arn)
        )

        def _publish_batch():
            resp = sns_client.publish_batch(
                TopicArn=topic_arn,
                PublishBatchRequestEntries=[
                    {"Id": "msg-1", "Message": "Batch message 1"},
                    {"Id": "msg-2", "Message": "Batch message 2"},
                    {"Id": "msg-3", "Message": "Batch message 3"},
                ],
            )
            assert len(resp.get("Successful", [])) == 3

        results.append(await runner.run_test("sns", "PublishBatch", _publish_batch))

        def _tag_resource():
            sns_client.tag_resource(
                ResourceArn=topic_arn, Tags=[{"Key": "Team", "Value": "Platform"}]
            )

        results.append(await runner.run_test("sns", "TagResource", _tag_resource))

        def _list_tags_for_resource():
            resp = sns_client.list_tags_for_resource(ResourceArn=topic_arn)
            assert resp.get("Tags") is not None
            tags_dict = {t["Key"]: t["Value"] for t in resp["Tags"]}
            assert tags_dict.get("Team") == "Platform"

        results.append(
            await runner.run_test("sns", "ListTagsForResource", _list_tags_for_resource)
        )

        def _untag_resource():
            sns_client.untag_resource(ResourceArn=topic_arn, TagKeys=["Team"])

        results.append(await runner.run_test("sns", "UntagResource", _untag_resource))

        def _unsubscribe():
            sns_client.unsubscribe(SubscriptionArn=subscription_arn)

        results.append(await runner.run_test("sns", "Unsubscribe", _unsubscribe))

        def _delete_topic():
            sns_client.delete_topic(TopicArn=topic_arn)

        results.append(await runner.run_test("sns", "DeleteTopic", _delete_topic))

    finally:
        try:
            if topic_arn:
                sns_client.delete_topic(TopicArn=topic_arn)
        except Exception:
            pass
        try:
            if queue_url:
                sqs_client.delete_queue(QueueUrl=queue_url)
        except Exception:
            pass

    def _create_topic_duplicate():
        resp = sns_client.create_topic(Name=topic_name)
        assert resp.get("TopicArn"), (
            "TopicArn is null (CreateTopic should be idempotent)"
        )

    results.append(
        await runner.run_test("sns", "CreateTopic_Duplicate", _create_topic_duplicate)
    )

    def _get_topic_attributes_nonexistent():
        try:
            sns_client.get_topic_attributes(
                TopicArn="arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NotFound"

    results.append(
        await runner.run_test(
            "sns", "GetTopicAttributes_NonExistent", _get_topic_attributes_nonexistent
        )
    )

    def _publish_nonexistent():
        try:
            sns_client.publish(
                TopicArn="arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345",
                Message="test",
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NotFound"

    results.append(
        await runner.run_test("sns", "Publish_NonExistent", _publish_nonexistent)
    )

    def _subscribe_nonexistent():
        try:
            sns_client.subscribe(
                TopicArn="arn:aws:sns:us-east-1:000000000000:NonExistentTopic_xyz_12345",
                Protocol="sqs",
                Endpoint="https://example.sqs.amazonaws.com/000000000000/test",
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NotFound"

    results.append(
        await runner.run_test("sns", "Subscribe_NonExistent", _subscribe_nonexistent)
    )

    def _multi_byte_publish():
        ja_msg = "日本語テストメッセージ"
        zh_msg = "简体中文测试消息"
        tw_msg = "繁體中文測試訊息"
        for msg in [ja_msg, zh_msg, tw_msg]:
            resp = sns_client.publish(TopicArn=topic_arn, Message=msg)
            assert resp.get("MessageId"), "MessageId is null"

    results.append(
        await runner.run_test("sns", "MultiBytePublish", _multi_byte_publish)
    )

    return results
