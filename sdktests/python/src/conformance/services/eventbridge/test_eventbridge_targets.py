def test_put_targets(eventbridge_setup, eventbridge_client):
    eventbridge_client.put_targets(
        Rule=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
        Targets=[
            {
                "Id": eventbridge_setup["target_id"],
                "Arn": f"arn:aws:lambda:us-east-1:000000000000:function:{eventbridge_setup['target_id']}",
            }
        ],
    )


def test_list_targets_by_rule(eventbridge_setup, eventbridge_client):
    eventbridge_client.put_targets(
        Rule=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
        Targets=[
            {
                "Id": eventbridge_setup["target_id"],
                "Arn": f"arn:aws:lambda:us-east-1:000000000000:function:{eventbridge_setup['target_id']}",
            }
        ],
    )
    resp = eventbridge_client.list_targets_by_rule(
        Rule=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
    )
    assert resp.get("Targets") is not None


def test_remove_targets(eventbridge_setup, eventbridge_client):
    eventbridge_client.put_targets(
        Rule=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
        Targets=[
            {
                "Id": eventbridge_setup["target_id"],
                "Arn": f"arn:aws:lambda:us-east-1:000000000000:function:{eventbridge_setup['target_id']}",
            }
        ],
    )
    eventbridge_client.remove_targets(
        Rule=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
        Ids=[eventbridge_setup["target_id"]],
    )


def test_list_rule_names_by_target(eventbridge_setup, eventbridge_client):
    eventbridge_client.put_targets(
        Rule=eventbridge_setup["rule_name"],
        EventBusName=eventbridge_setup["event_bus_name"],
        Targets=[
            {
                "Id": eventbridge_setup["target_id"],
                "Arn": f"arn:aws:lambda:us-east-1:000000000000:function:{eventbridge_setup['target_id']}",
            }
        ],
    )
    eventbridge_client.list_rule_names_by_target(
        TargetArn=f"arn:aws:lambda:us-east-1:000000000000:function:{eventbridge_setup['target_id']}"
    )
