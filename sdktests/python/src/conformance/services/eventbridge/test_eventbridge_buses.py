def test_create_event_bus(eventbridge_client, unique_name):
    bus_name = unique_name("PyEventBus")
    resp = eventbridge_client.create_event_bus(Name=bus_name)
    assert resp.get("EventBusArn"), "EventBusArn is null"
    try:
        eventbridge_client.delete_event_bus(Name=bus_name)
    except Exception:
        pass


def test_describe_event_bus(eventbridge_setup, eventbridge_client):
    resp = eventbridge_client.describe_event_bus(
        Name=eventbridge_setup["event_bus_name"]
    )
    arn = resp.get("EventBusArn") or resp.get("Arn")
    assert arn, "EventBusArn is null"


def test_list_event_buses(eventbridge_client):
    resp = eventbridge_client.list_event_buses()
    assert resp.get("EventBuses") is not None


def test_delete_event_bus(eventbridge_client, unique_name):
    bus_name = unique_name("PyEventBus")
    eventbridge_client.create_event_bus(Name=bus_name)
    eventbridge_client.delete_event_bus(Name=bus_name)
