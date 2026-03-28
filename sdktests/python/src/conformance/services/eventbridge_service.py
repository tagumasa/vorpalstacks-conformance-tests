import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_eventbridge_tests(
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
    eb_client = session.client("events", endpoint_url=endpoint, region_name=region)

    rule_name = _make_unique_name("PyRule")
    event_bus_name = _make_unique_name("PyEventBus")
    target_id = _make_unique_name("PyTarget")
    rule_created = False
    event_bus_created = False

    sample_event = {
        "Source": "com.example.sdk",
        "DetailType": "TestEvent",
        "Detail": json.dumps({"message": "Hello from SDK test"}),
    }

    try:

        def _create_event_bus():
            nonlocal event_bus_created
            resp = eb_client.create_event_bus(Name=event_bus_name)
            assert resp.get("EventBusArn"), "EventBusArn is null"
            event_bus_created = True

        results.append(
            await runner.run_test("eventbridge", "CreateEventBus", _create_event_bus)
        )

        def _describe_event_bus():
            resp = eb_client.describe_event_bus(Name=event_bus_name)
            arn = resp.get("EventBusArn") or resp.get("Arn")
            assert arn, "EventBusArn is null"

        results.append(
            await runner.run_test(
                "eventbridge", "DescribeEventBus", _describe_event_bus
            )
        )

        def _list_event_buses():
            resp = eb_client.list_event_buses()
            assert resp.get("EventBuses") is not None

        results.append(
            await runner.run_test("eventbridge", "ListEventBuses", _list_event_buses)
        )

        def _put_rule():
            nonlocal rule_created
            resp = eb_client.put_rule(
                Name=rule_name,
                EventBusName=event_bus_name,
                State="ENABLED",
                Description="Test rule for SDK tests",
            )
            assert resp.get("RuleArn"), "RuleArn is null"
            rule_created = True

        results.append(await runner.run_test("eventbridge", "PutRule", _put_rule))

        def _describe_rule():
            resp = eb_client.describe_rule(Name=rule_name, EventBusName=event_bus_name)
            arn = resp.get("RuleArn") or resp.get("Arn")
            assert arn, "RuleArn is null"
            assert resp["Name"] == rule_name

        results.append(
            await runner.run_test("eventbridge", "DescribeRule", _describe_rule)
        )

        def _list_rules():
            resp = eb_client.list_rules(EventBusName=event_bus_name)
            assert resp.get("Rules") is not None

        results.append(await runner.run_test("eventbridge", "ListRules", _list_rules))

        def _disable_rule():
            eb_client.disable_rule(Name=rule_name, EventBusName=event_bus_name)

        results.append(
            await runner.run_test("eventbridge", "DisableRule", _disable_rule)
        )

        def _enable_rule():
            eb_client.enable_rule(Name=rule_name, EventBusName=event_bus_name)

        results.append(await runner.run_test("eventbridge", "EnableRule", _enable_rule))

        def _put_targets():
            eb_client.put_targets(
                Rule=rule_name,
                EventBusName=event_bus_name,
                Targets=[
                    {
                        "Id": target_id,
                        "Arn": f"arn:aws:lambda:us-east-1:000000000000:function:{target_id}",
                    }
                ],
            )

        results.append(await runner.run_test("eventbridge", "PutTargets", _put_targets))

        def _list_targets_by_rule():
            resp = eb_client.list_targets_by_rule(
                Rule=rule_name, EventBusName=event_bus_name
            )
            assert resp.get("Targets") is not None

        results.append(
            await runner.run_test(
                "eventbridge", "ListTargetsByRule", _list_targets_by_rule
            )
        )

        def _list_rule_names_by_target():
            eb_client.list_rule_names_by_target(
                TargetArn=f"arn:aws:lambda:us-east-1:000000000000:function:{target_id}"
            )

        results.append(
            await runner.run_test(
                "eventbridge", "ListRuleNamesByTarget", _list_rule_names_by_target
            )
        )

        def _put_events():
            resp = eb_client.put_events(Entries=[sample_event])
            assert resp.get("FailedEntryCount") is not None, "FailedEntryCount is null"

        results.append(await runner.run_test("eventbridge", "PutEvents", _put_events))

        def _remove_targets():
            eb_client.remove_targets(
                Rule=rule_name, EventBusName=event_bus_name, Ids=[target_id]
            )

        results.append(
            await runner.run_test("eventbridge", "RemoveTargets", _remove_targets)
        )

        rule_arn = (
            f"arn:aws:events:{region}:000000000000:rule/{event_bus_name}/{rule_name}"
        )

        def _tag_resource():
            eb_client.tag_resource(
                ResourceARN=rule_arn,
                Tags=[{"Key": "Environment", "Value": "test"}],
            )

        results.append(
            await runner.run_test("eventbridge", "TagResource", _tag_resource)
        )

        def _list_tags_for_resource():
            resp = eb_client.list_tags_for_resource(ResourceARN=rule_arn)
            assert resp.get("Tags") is not None

        results.append(
            await runner.run_test(
                "eventbridge", "ListTagsForResource", _list_tags_for_resource
            )
        )

        def _untag_resource():
            eb_client.untag_resource(ResourceARN=rule_arn, TagKeys=["Environment"])

        results.append(
            await runner.run_test("eventbridge", "UntagResource", _untag_resource)
        )

        def _delete_rule():
            nonlocal rule_created
            eb_client.delete_rule(Name=rule_name, EventBusName=event_bus_name)
            rule_created = False

        results.append(await runner.run_test("eventbridge", "DeleteRule", _delete_rule))

        def _delete_event_bus():
            nonlocal event_bus_created
            eb_client.delete_event_bus(Name=event_bus_name)
            event_bus_created = False

        results.append(
            await runner.run_test("eventbridge", "DeleteEventBus", _delete_event_bus)
        )

    finally:
        try:
            if rule_created:
                eb_client.remove_targets(
                    Rule=rule_name, EventBusName=event_bus_name, Ids=[target_id]
                )
                eb_client.delete_rule(Name=rule_name, EventBusName=event_bus_name)
        except Exception:
            pass
        try:
            if event_bus_created:
                eb_client.delete_event_bus(Name=event_bus_name)
        except Exception:
            pass

    def _describe_rule_nonexistent():
        try:
            eb_client.describe_rule(Name="NonExistentRule_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "eventbridge", "DescribeRule_NonExistent", _describe_rule_nonexistent
        )
    )

    def _delete_rule_nonexistent():
        try:
            eb_client.delete_rule(Name="nonexistent-rule-xyz-12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "eventbridge", "DeleteRule_NonExistent", _delete_rule_nonexistent
        )
    )

    def _describe_event_bus_nonexistent():
        try:
            eb_client.describe_event_bus(Name="nonexistent-bus-xyz-12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "eventbridge",
            "DescribeEventBus_NonExistent",
            _describe_event_bus_nonexistent,
        )
    )

    def _delete_event_bus_nonexistent():
        try:
            eb_client.delete_event_bus(Name="nonexistent-bus-xyz-12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "eventbridge",
            "DeleteEventBus_NonExistent",
            _delete_event_bus_nonexistent,
        )
    )

    def _create_event_bus_duplicate():
        dup_bus = _make_unique_name("PyDupBus")
        eb_client.create_event_bus(Name=dup_bus)
        try:
            eb_client.create_event_bus(Name=dup_bus)
            raise AssertionError("expected error for duplicate event bus name")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceAlreadyExistsException"
        finally:
            try:
                eb_client.delete_event_bus(Name=dup_bus)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "eventbridge",
            "CreateEventBus_DuplicateName",
            _create_event_bus_duplicate,
        )
    )

    def _put_rule_disable_and_verify():
        rd_bus = _make_unique_name("PyRdBus")
        rd_rule = _make_unique_name("PyRdRule")
        eb_client.create_event_bus(Name=rd_bus)
        try:
            eb_client.put_rule(
                Name=rd_rule,
                EventBusName=rd_bus,
                Description="test rule for disable",
            )
            eb_client.disable_rule(Name=rd_rule, EventBusName=rd_bus)
            resp = eb_client.describe_rule(Name=rd_rule, EventBusName=rd_bus)
            assert resp["State"] == "DISABLED", (
                f"expected state DISABLED, got {resp['State']}"
            )
            eb_client.enable_rule(Name=rd_rule, EventBusName=rd_bus)
            resp2 = eb_client.describe_rule(Name=rd_rule, EventBusName=rd_bus)
            assert resp2["State"] == "ENABLED", (
                f"expected state ENABLED, got {resp2['State']}"
            )
        finally:
            try:
                eb_client.delete_rule(Name=rd_rule, EventBusName=rd_bus)
            except Exception:
                pass
            try:
                eb_client.delete_event_bus(Name=rd_bus)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "eventbridge",
            "PutRule_DisableAndVerify",
            _put_rule_disable_and_verify,
        )
    )

    def _put_rule_with_event_pattern():
        ep_bus = _make_unique_name("PyEpBus")
        ep_rule = _make_unique_name("PyEpRule")
        eb_client.create_event_bus(Name=ep_bus)
        try:
            pattern = json.dumps(
                {
                    "source": ["com.example.test"],
                    "detail-type": ["OrderCreated"],
                }
            )
            eb_client.put_rule(
                Name=ep_rule,
                EventBusName=ep_bus,
                EventPattern=pattern,
            )
            resp = eb_client.describe_rule(Name=ep_rule, EventBusName=ep_bus)
            assert resp.get("EventPattern"), "event pattern is nil"
        finally:
            try:
                eb_client.delete_rule(Name=ep_rule, EventBusName=ep_bus)
            except Exception:
                pass
            try:
                eb_client.delete_event_bus(Name=ep_bus)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "eventbridge",
            "PutRule_WithEventPattern",
            _put_rule_with_event_pattern,
        )
    )

    def _put_events_default_bus():
        event = json.dumps(
            {
                "source": "com.test.default",
                "detail-type": "DefaultBusEvent",
                "detail": {"key": "value"},
            }
        )
        resp = eb_client.put_events(
            Entries=[
                {
                    "Source": "com.test.default",
                    "DetailType": "DefaultBusEvent",
                    "Detail": event,
                }
            ]
        )
        assert resp.get("FailedEntryCount", 0) == 0
        assert len(resp.get("Entries", [])) == 1
        assert resp["Entries"][0].get("EventId"), "expected non-empty event ID"

    results.append(
        await runner.run_test(
            "eventbridge", "PutEvents_DefaultBus", _put_events_default_bus
        )
    )

    def _put_targets_remove_targets_verify():
        tr_bus = _make_unique_name("PyTrBus")
        tr_rule = _make_unique_name("PyTrRule")
        tr_target = _make_unique_name("PyTrTarget")
        eb_client.create_event_bus(Name=tr_bus)
        try:
            eb_client.put_rule(Name=tr_rule, EventBusName=tr_bus)
            target_arn = f"arn:aws:lambda:{region}:000000000000:function:TargetFunc"
            eb_client.put_targets(
                Rule=tr_rule,
                EventBusName=tr_bus,
                Targets=[
                    {
                        "Id": tr_target,
                        "Arn": target_arn,
                        "Input": '{"action": "test"}',
                    }
                ],
            )
            list_resp = eb_client.list_targets_by_rule(
                Rule=tr_rule, EventBusName=tr_bus
            )
            assert len(list_resp.get("Targets", [])) == 1
            assert list_resp["Targets"][0]["Arn"] == target_arn
            assert list_resp["Targets"][0]["Input"] == '{"action": "test"}'

            eb_client.remove_targets(Rule=tr_rule, EventBusName=tr_bus, Ids=[tr_target])
            list_resp2 = eb_client.list_targets_by_rule(
                Rule=tr_rule, EventBusName=tr_bus
            )
            assert len(list_resp2.get("Targets", [])) == 0
        finally:
            try:
                eb_client.delete_rule(Name=tr_rule, EventBusName=tr_bus)
            except Exception:
                pass
            try:
                eb_client.delete_event_bus(Name=tr_bus)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "eventbridge",
            "PutTargets_RemoveTargets_Verify",
            _put_targets_remove_targets_verify,
        )
    )

    def _delete_rule_with_targets_fails():
        dt_bus = _make_unique_name("PyDtBus")
        dt_rule = _make_unique_name("PyDtRule")
        dt_target = _make_unique_name("PyDtTarget")
        eb_client.create_event_bus(Name=dt_bus)
        try:
            eb_client.put_rule(Name=dt_rule, EventBusName=dt_bus)
            eb_client.put_targets(
                Rule=dt_rule,
                EventBusName=dt_bus,
                Targets=[
                    {
                        "Id": dt_target,
                        "Arn": f"arn:aws:lambda:{region}:000000000000:function:F",
                    }
                ],
            )
            try:
                eb_client.delete_rule(Name=dt_rule, EventBusName=dt_bus)
                raise AssertionError("expected error when deleting rule with targets")
            except ClientError:
                pass
        finally:
            try:
                eb_client.remove_targets(
                    Rule=dt_rule, EventBusName=dt_bus, Ids=[dt_target]
                )
            except Exception:
                pass
            try:
                eb_client.delete_rule(Name=dt_rule, EventBusName=dt_bus)
            except Exception:
                pass
            try:
                eb_client.delete_event_bus(Name=dt_bus)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "eventbridge",
            "DeleteRule_WithTargetsFails",
            _delete_rule_with_targets_fails,
        )
    )

    def _put_targets_nonexistent_rule():
        try:
            eb_client.put_targets(
                Rule="NonExistentRule_xyz_12345",
                Targets=[
                    {
                        "Id": "some-target",
                        "Arn": "arn:aws:lambda:us-east-1:000000000000:function:some-func",
                    }
                ],
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "eventbridge",
            "PutTargets_NonExistentRule",
            _put_targets_nonexistent_rule,
        )
    )

    return results
