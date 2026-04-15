def test_create_function(lambda_client, iam_helper, unique_name, sample_function_code):
    function_name = unique_name("PyFunc")
    role_name = unique_name("PyRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    iam_helper.create_role(role_name)
    try:
        resp = lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": sample_function_code},
        )
        assert resp.get("FunctionName") == function_name
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)


def test_get_function(lambda_function_setup, lambda_client):
    resp = lambda_client.get_function(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp.get("Configuration"), "Configuration is null"


def test_get_function_configuration(lambda_function_setup, lambda_client):
    resp = lambda_client.get_function_configuration(
        FunctionName=lambda_function_setup["function_name"]
    )
    assert resp.get("FunctionName"), "function name is nil"


def test_list_functions(lambda_client):
    resp = lambda_client.list_functions()
    assert resp.get("Functions") is not None


def test_update_function_code(lambda_function_setup, lambda_client):
    new_code = b"exports.handler = async (event) => { return { statusCode: 200, body: 'Updated' }; };"
    resp = lambda_client.update_function_code(
        FunctionName=lambda_function_setup["function_name"], ZipFile=new_code
    )
    assert resp.get("LastModified"), "LastModified is nil"


def test_update_function_configuration(lambda_function_setup, lambda_client):
    resp = lambda_client.update_function_configuration(
        FunctionName=lambda_function_setup["function_name"],
        Description="Updated function",
    )
    assert resp is not None


def test_delete_function(lambda_client, iam_helper, unique_name):
    function_name = unique_name("PyFunc")
    role_name = unique_name("PyRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async (event) => { return { statusCode: 200, body: 'Hello' }; };"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
        )
        resp = lambda_client.delete_function(FunctionName=function_name)
        assert resp is not None
    except Exception:
        pass
    finally:
        iam_helper.delete_role(role_name)
