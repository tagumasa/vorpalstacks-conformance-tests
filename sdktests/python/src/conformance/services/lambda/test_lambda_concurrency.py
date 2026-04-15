def test_put_function_concurrency(lambda_function_setup, lambda_client):
    resp = lambda_client.put_function_concurrency(
        FunctionName=lambda_function_setup["function_name"],
        ReservedConcurrentExecutions=10,
    )
    assert resp is not None


def test_get_function_concurrency(lambda_function_setup, lambda_client):
    lambda_client.put_function_concurrency(
        FunctionName=lambda_function_setup["function_name"],
        ReservedConcurrentExecutions=10,
    )
    resp = lambda_client.get_function_concurrency(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp.get("ReservedConcurrentExecutions") is not None


def test_delete_function_concurrency(lambda_function_setup, lambda_client):
    lambda_client.put_function_concurrency(
        FunctionName=lambda_function_setup["function_name"],
        ReservedConcurrentExecutions=10,
    )
    resp = lambda_client.delete_function_concurrency(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp is not None
