def test_tag_resource(lambda_function_setup, lambda_client):
    lambda_client.tag_resource(
        Resource=lambda_function_setup["function_arn"],
        Tags={"Environment": "test", "Project": "sdk-tests"},
    )


def test_list_tags(lambda_function_setup, lambda_client):
    lambda_client.tag_resource(
        Resource=lambda_function_setup["function_arn"],
        Tags={"Environment": "test", "Project": "sdk-tests"},
    )
    resp = lambda_client.list_tags(Resource=lambda_function_setup["function_arn"])
    assert resp.get("Tags") is not None


def test_untag_resource(lambda_function_setup, lambda_client):
    lambda_client.tag_resource(
        Resource=lambda_function_setup["function_arn"],
        Tags={"Environment": "test"},
    )
    lambda_client.untag_resource(
        Resource=lambda_function_setup["function_arn"], TagKeys=["Environment"]
    )
