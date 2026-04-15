def test_invoke(lambda_function_setup, lambda_client):
    resp = lambda_client.invoke(FunctionName=lambda_function_setup["function_name"])
    assert resp.get("StatusCode") != 0


def test_invoke_verify_response_payload(lambda_client, iam_helper, unique_name):
    function_name = unique_name("InvFunc")
    role_name = unique_name("InvRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b'exports.handler = async (event) => { return { statusCode: 200, body: JSON.stringify({result: "ok"}) }; };'
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
        )
        resp = lambda_client.invoke(FunctionName=function_name)
        assert resp["StatusCode"] == 200
        payload = resp["Payload"].read()
        assert len(payload) > 0, "payload should not be empty"
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)
