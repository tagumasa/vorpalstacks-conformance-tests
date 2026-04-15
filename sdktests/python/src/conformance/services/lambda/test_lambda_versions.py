def test_publish_version(lambda_function_setup, lambda_client):
    resp = lambda_client.publish_version(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp.get("Version"), "Version is nil"


def test_list_versions_by_function(lambda_function_setup, lambda_client):
    resp = lambda_client.list_versions_by_function(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp.get("Versions") is not None
