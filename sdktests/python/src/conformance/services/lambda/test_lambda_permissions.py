def test_add_permission(lambda_function_setup, lambda_client, unique_name):
    statement_id = unique_name("stmt")
    resp = lambda_client.add_permission(
        FunctionName=lambda_function_setup["function_name"],
        StatementId=statement_id,
        Action="lambda:InvokeFunction",
        Principal="apigateway.amazonaws.com",
    )
    assert resp is not None


def test_get_policy(lambda_function_setup, lambda_client, unique_name):
    statement_id = unique_name("stmt")
    lambda_client.add_permission(
        FunctionName=lambda_function_setup["function_name"],
        StatementId=statement_id,
        Action="lambda:InvokeFunction",
        Principal="apigateway.amazonaws.com",
    )
    resp = lambda_client.get_policy(FunctionName=lambda_function_setup["function_name"])
    assert resp.get("Policy"), "policy is empty"


def test_remove_permission(lambda_function_setup, lambda_client, unique_name):
    statement_id = unique_name("stmt")
    lambda_client.add_permission(
        FunctionName=lambda_function_setup["function_name"],
        StatementId=statement_id,
        Action="lambda:InvokeFunction",
        Principal="apigateway.amazonaws.com",
    )
    lambda_client.remove_permission(
        FunctionName=lambda_function_setup["function_name"], StatementId=statement_id
    )
