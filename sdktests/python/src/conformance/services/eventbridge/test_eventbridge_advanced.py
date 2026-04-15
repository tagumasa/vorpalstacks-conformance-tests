import json


def test_put_rule_disable_and_verify(eventbridge_client, unique_name):
    bus_name = unique_name("PyRdBus")
    rule_name = unique_name("PyRdRule")
    eventbridge_client.create_event_bus(Name=bus_name)
    try:
        eventbridge_client.put_rule(
            Name=rule_name,
            EventBusName=bus_name,
            Description="test rule for disable",
        )
        eventbridge_client.disable_rule(Name=rule_name, EventBusName=bus_name)
        resp = eventbridge_client.describe_rule(Name=rule_name, EventBusName=bus_name)
        assert resp["State"] == "DISABLED", (
            f"expected state DISABLED, got {resp['State']}"
        )
        eventbridge_client.enable_rule(Name=rule_name, EventBusName=bus_name)
        resp2 = eventbridge_client.describe_rule(Name=rule_name, EventBusName=bus_name)
        assert resp2["State"] == "ENABLED", (
            f"expected state ENABLED, got {resp2['State']}"
        )
    finally:
        try:
            eventbridge_client.delete_rule(Name=rule_name, EventBusName=bus_name)
        except Exception:
            pass
        try:
            eventbridge_client.delete_event_bus(Name=bus_name)
        except Exception:
            pass


def test_put_rule_with_event_pattern(eventbridge_client, unique_name):
    bus_name = unique_name("PyEpBus")
    rule_name = unique_name("PyEpRule")
    eventbridge_client.create_event_bus(Name=bus_name)
    try:
        pattern = json.dumps(
            {
                "source": ["com.example.test"],
                "detail-type": ["OrderCreated"],
            }
        )
        eventbridge_client.put_rule(
            Name=rule_name,
            EventBusName=bus_name,
            EventPattern=pattern,
        )
        resp = eventbridge_client.describe_rule(Name=rule_name, EventBusName=bus_name)
        assert resp.get("EventPattern"), "event pattern is nil"
    finally:
        try:
            eventbridge_client.delete_rule(Name=rule_name, EventBusName=bus_name)
        except Exception:
            pass
        try:
            eventbridge_client.delete_event_bus(Name=bus_name)
        except Exception:
            pass


def test_put_targets_remove_targets_verify(eventbridge_client, unique_name, region):
    bus_name = unique_name("PyTrBus")
    rule_name = unique_name("PyTrRule")
    target_id = unique_name("PyTrTarget")
    eventbridge_client.create_event_bus(Name=bus_name)
    try:
        eventbridge_client.put_rule(Name=rule_name, EventBusName=bus_name)
        target_arn = f"arn:aws:lambda:{region}:000000000000:function:TargetFunc"
        eventbridge_client.put_targets(
            Rule=rule_name,
            EventBusName=bus_name,
            Targets=[
                {
                    "Id": target_id,
                    "Arn": target_arn,
                    "Input": '{"action": "test"}',
                }
            ],
        )
        list_resp = eventbridge_client.list_targets_by_rule(
            Rule=rule_name, EventBusName=bus_name
        )
        assert len(list_resp.get("Targets", [])) == 1
        assert list_resp["Targets"][0]["Arn"] == target_arn
        assert list_resp["Targets"][0]["Input"] == '{"action": "test"}'

        eventbridge_client.remove_targets(
            Rule=rule_name, EventBusName=bus_name, Ids=[target_id]
        )
        list_resp2 = eventbridge_client.list_targets_by_rule(
            Rule=rule_name, EventBusName=bus_name
        )
        assert len(list_resp2.get("Targets", [])) == 0
    finally:
        try:
            eventbridge_client.delete_rule(Name=rule_name, EventBusName=bus_name)
        except Exception:
            pass
        try:
            eventbridge_client.delete_event_bus(Name=bus_name)
        except Exception:
            pass
