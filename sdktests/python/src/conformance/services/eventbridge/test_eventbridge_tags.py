def test_tag_resource(eventbridge_setup, eventbridge_client):
    eventbridge_client.tag_resource(
        ResourceARN=eventbridge_setup["rule_arn"],
        Tags=[{"Key": "Environment", "Value": "test"}],
    )


def test_list_tags_for_resource(eventbridge_setup, eventbridge_client):
    eventbridge_client.tag_resource(
        ResourceARN=eventbridge_setup["rule_arn"],
        Tags=[{"Key": "Environment", "Value": "test"}],
    )
    resp = eventbridge_client.list_tags_for_resource(
        ResourceARN=eventbridge_setup["rule_arn"]
    )
    assert resp.get("Tags") is not None


def test_untag_resource(eventbridge_setup, eventbridge_client):
    eventbridge_client.tag_resource(
        ResourceARN=eventbridge_setup["rule_arn"],
        Tags=[{"Key": "Environment", "Value": "test"}],
    )
    eventbridge_client.untag_resource(
        ResourceARN=eventbridge_setup["rule_arn"], TagKeys=["Environment"]
    )
