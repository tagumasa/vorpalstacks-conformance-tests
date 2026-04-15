import json

import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


def test_describe_rule_nonexistent(eventbridge_client):
    with pytest.raises(ClientError) as exc:
        eventbridge_client.describe_rule(Name="NonExistentRule_xyz_12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_delete_rule_nonexistent(eventbridge_client):
    with pytest.raises(ClientError) as exc:
        eventbridge_client.delete_rule(Name="nonexistent-rule-xyz-12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_describe_event_bus_nonexistent(eventbridge_client):
    with pytest.raises(ClientError) as exc:
        eventbridge_client.describe_event_bus(Name="nonexistent-bus-xyz-12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_delete_event_bus_nonexistent(eventbridge_client):
    with pytest.raises(ClientError) as exc:
        eventbridge_client.delete_event_bus(Name="nonexistent-bus-xyz-12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_create_event_bus_duplicate(eventbridge_client, unique_name):
    dup_bus = unique_name("PyDupBus")
    eventbridge_client.create_event_bus(Name=dup_bus)
    try:
        with pytest.raises(ClientError) as exc:
            eventbridge_client.create_event_bus(Name=dup_bus)
        assert_client_error(exc, "ResourceAlreadyExistsException")
    finally:
        try:
            eventbridge_client.delete_event_bus(Name=dup_bus)
        except Exception:
            pass


def test_delete_rule_with_targets_fails(eventbridge_client, unique_name):
    bus_name = unique_name("PyDtBus")
    rule_name = unique_name("PyDtRule")
    target_id = unique_name("PyDtTarget")
    eventbridge_client.create_event_bus(Name=bus_name)
    try:
        eventbridge_client.put_rule(Name=rule_name, EventBusName=bus_name)
        eventbridge_client.put_targets(
            Rule=rule_name,
            EventBusName=bus_name,
            Targets=[
                {
                    "Id": target_id,
                    "Arn": "arn:aws:lambda:us-east-1:000000000000:function:F",
                }
            ],
        )
        with pytest.raises(ClientError):
            eventbridge_client.delete_rule(Name=rule_name, EventBusName=bus_name)
    finally:
        try:
            eventbridge_client.remove_targets(
                Rule=rule_name, EventBusName=bus_name, Ids=[target_id]
            )
        except Exception:
            pass
        try:
            eventbridge_client.delete_rule(Name=rule_name, EventBusName=bus_name)
        except Exception:
            pass
        try:
            eventbridge_client.delete_event_bus(Name=bus_name)
        except Exception:
            pass


def test_put_targets_nonexistent_rule(eventbridge_client):
    with pytest.raises(ClientError) as exc:
        eventbridge_client.put_targets(
            Rule="NonExistentRule_xyz_12345",
            Targets=[
                {
                    "Id": "some-target",
                    "Arn": "arn:aws:lambda:us-east-1:000000000000:function:some-func",
                }
            ],
        )
    assert_client_error(exc, "ResourceNotFoundException")
