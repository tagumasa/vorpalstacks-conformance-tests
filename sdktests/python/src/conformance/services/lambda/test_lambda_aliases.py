def test_create_alias(lambda_function_setup, lambda_client):
    resp = lambda_client.create_alias(
        FunctionName=lambda_function_setup["function_name"],
        Name="live",
        FunctionVersion="$LATEST",
    )
    assert resp.get("AliasArn"), "AliasArn is nil"


def test_get_alias(lambda_function_setup, lambda_client):
    try:
        lambda_client.delete_alias(
            FunctionName=lambda_function_setup["function_name"], Name="live"
        )
    except Exception:
        pass
    lambda_client.create_alias(
        FunctionName=lambda_function_setup["function_name"],
        Name="live",
        FunctionVersion="$LATEST",
    )
    resp = lambda_client.get_alias(
        FunctionName=lambda_function_setup["function_name"], Name="live"
    )
    assert resp.get("Name"), "alias name is nil"


def test_update_alias(lambda_function_setup, lambda_client):
    try:
        lambda_client.delete_alias(
            FunctionName=lambda_function_setup["function_name"], Name="live"
        )
    except Exception:
        pass
    lambda_client.create_alias(
        FunctionName=lambda_function_setup["function_name"],
        Name="live",
        FunctionVersion="$LATEST",
    )
    resp = lambda_client.update_alias(
        FunctionName=lambda_function_setup["function_name"],
        Name="live",
        Description="Production alias",
    )
    assert resp is not None


def test_list_aliases(lambda_function_setup, lambda_client):
    try:
        lambda_client.delete_alias(
            FunctionName=lambda_function_setup["function_name"], Name="live"
        )
    except Exception:
        pass
    lambda_client.create_alias(
        FunctionName=lambda_function_setup["function_name"],
        Name="live",
        FunctionVersion="$LATEST",
    )
    resp = lambda_client.list_aliases(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp.get("Aliases") is not None


def test_delete_alias(lambda_function_setup, lambda_client):
    try:
        lambda_client.delete_alias(
            FunctionName=lambda_function_setup["function_name"], Name="live"
        )
    except Exception:
        pass
    lambda_client.create_alias(
        FunctionName=lambda_function_setup["function_name"],
        Name="live",
        FunctionVersion="$LATEST",
    )
    resp = lambda_client.delete_alias(
        FunctionName=lambda_function_setup["function_name"], Name="live"
    )
    assert resp is not None
