def test_put_rule(eventbridge_setup, eventbridge_client):
    resp = eventbridge_client.describe_rule(
        Name=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
    )
    assert resp.get("RuleArn") or resp.get("Arn"), "RuleArn is null"


def test_describe_rule(eventbridge_setup, eventbridge_client):
    resp = eventbridge_client.describe_rule(
        Name=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
    )
    arn = resp.get("RuleArn") or resp.get("Arn")
    assert arn, "RuleArn is null"
    assert resp["Name"] == eventbridge_setup["rule_name"]


def test_list_rules(eventbridge_setup, eventbridge_client):
    resp = eventbridge_client.list_rules(
        EventBusName=eventbridge_setup["event_bus_name"]
    )
    assert resp.get("Rules") is not None


def test_disable_rule(eventbridge_setup, eventbridge_client):
    eventbridge_client.disable_rule(
        Name=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
    )


def test_enable_rule(eventbridge_setup, eventbridge_client):
    eventbridge_client.enable_rule(
        Name=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
    )


def test_delete_rule(eventbridge_client, unique_name):
    bus_name = unique_name("PyEventBus")
    rule_name = unique_name("PyRule")
    eventbridge_client.create_event_bus(Name=bus_name)
    try:
        eventbridge_client.put_rule(
            Name=rule_name,
            EventBusName=bus_name,
        )
        eventbridge_client.delete_rule(Name=rule_name, EventBusName=bus_name)
    finally:
        try:
            eventbridge_client.delete_event_bus(Name=bus_name)
        except Exception:
            pass
