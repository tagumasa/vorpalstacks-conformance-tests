import pytest


@pytest.fixture(scope="module")
def eventbridge_client(aws_session, endpoint, region):
    return aws_session.client("events", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def eventbridge_setup(eventbridge_client, unique_name, region):
    event_bus_name = unique_name("PyEventBus")
    rule_name = unique_name("PyRule")
    target_id = unique_name("PyTarget")
    resp = eventbridge_client.create_event_bus(Name=event_bus_name)
    rule_arn = eventbridge_client.put_rule(
        Name=rule_name,
        EventBusName=event_bus_name,
        State="ENABLED",
        Description="Test rule for SDK tests",
    )["RuleArn"]
    yield {
        "event_bus_name": event_bus_name,
        "rule_name": rule_name,
        "target_id": target_id,
        "rule_arn": rule_arn,
    }
    try:
        eventbridge_client.remove_targets(
            Rule=rule_name, EventBusName=event_bus_name, Ids=[target_id]
        )
    except Exception:
        pass
    try:
        eventbridge_client.delete_rule(Name=rule_name, EventBusName=event_bus_name)
    except Exception:
        pass
    try:
        eventbridge_client.delete_event_bus(Name=event_bus_name)
    except Exception:
        pass
