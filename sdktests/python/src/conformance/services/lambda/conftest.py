import json
import urllib.request

import pytest


@pytest.fixture(scope="module")
def lambda_client(aws_session, endpoint, region):
    return aws_session.client("lambda", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def iam_client(aws_session, endpoint, region):
    return aws_session.client("iam", endpoint_url=endpoint, region_name=region)


def _iam_role_http(endpoint, action, role_name, extra_form=""):
    form = f"Action={action}&Version=2010-05-08&RoleName={role_name}"
    if extra_form:
        form += f"&{extra_form}"
    data = form.encode("utf-8")
    req = urllib.request.Request(
        endpoint,
        data=data,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    try:
        urllib.request.urlopen(req)
    except Exception:
        pass


@pytest.fixture(scope="module")
def iam_helper(endpoint):
    class _Helper:
        def create_role(self, name):
            policy = json.dumps(
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Principal": {"Service": "lambda.amazonaws.com"},
                            "Action": "sts:AssumeRole",
                        }
                    ],
                }
            )
            _iam_role_http(
                endpoint,
                "CreateRole",
                name,
                f"AssumeRolePolicyDocument={urllib.parse.quote(policy)}",
            )

        def delete_role(self, name):
            _iam_role_http(endpoint, "DeleteRole", name)

    return _Helper()


@pytest.fixture
def sample_function_code():
    return b"exports.handler = async (event) => { return { statusCode: 200, body: 'Hello' }; };"


@pytest.fixture(scope="module")
def lambda_function_setup(lambda_client, iam_helper, unique_name, region):
    function_name = unique_name("PyFunc")
    role_name = unique_name("PyRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async (event) => { return { statusCode: 200, body: 'Hello' }; };"
    iam_helper.create_role(role_name)
    lambda_client.create_function(
        FunctionName=function_name,
        Runtime="nodejs22.x",
        Role=role_arn,
        Handler="index.handler",
        Code={"ZipFile": code},
    )
    function_arn = f"arn:aws:lambda:{region}:000000000000:function:{function_name}"
    yield {
        "function_name": function_name,
        "role_name": role_name,
        "role_arn": role_arn,
        "function_arn": function_arn,
        "code": code,
    }
    try:
        lambda_client.delete_function(FunctionName=function_name)
    except Exception:
        pass
    iam_helper.delete_role(role_name)
