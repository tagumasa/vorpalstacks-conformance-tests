import json


def test_put_events(eventbridge_client):
    sample_event = {
        "Source": "com.example.sdk",
        "DetailType": "TestEvent",
        "Detail": json.dumps({"message": "Hello from SDK test"}),
    }
    resp = eventbridge_client.put_events(Entries=[sample_event])
    assert resp.get("FailedEntryCount") is not None, "FailedEntryCount is null"


def test_put_events_default_bus(eventbridge_client):
    event = json.dumps(
        {
            "source": "com.test.default",
            "detail-type": "DefaultBusEvent",
            "detail": {"key": "value"},
        }
    )
    resp = eventbridge_client.put_events(
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
