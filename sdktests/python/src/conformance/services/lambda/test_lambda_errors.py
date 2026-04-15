import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


def test_get_function_nonexistent(lambda_client):
    with pytest.raises(ClientError) as exc:
        lambda_client.get_function(FunctionName="NoSuchFunction_xyz_12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_invoke_nonexistent(lambda_client):
    with pytest.raises(ClientError) as exc:
        lambda_client.invoke(FunctionName="NoSuchFunction_xyz_12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_update_function_code_nonexistent(lambda_client):
    with pytest.raises(ClientError) as exc:
        lambda_client.update_function_code(
            FunctionName="NoSuchFunction_xyz_12345", ZipFile=b"code"
        )
    assert_client_error(exc, "ResourceNotFoundException")


def test_delete_function_nonexistent(lambda_client):
    with pytest.raises(ClientError) as exc:
        lambda_client.delete_function(FunctionName="NoSuchFunction_xyz_12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_create_function_duplicate(lambda_client, iam_helper, unique_name):
    function_name = unique_name("DupFunc")
    role_name = unique_name("DupRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async () => { return 1; };"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
        )
        with pytest.raises(ClientError) as exc:
            lambda_client.create_function(
                FunctionName=function_name,
                Runtime="nodejs22.x",
                Role=role_arn,
                Handler="index.handler",
                Code={"ZipFile": code},
            )
        assert_client_error(exc, "ResourceConflictException")
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)


def test_get_function_contains_code_config(lambda_client, iam_helper, unique_name):
    function_name = unique_name("GfcFunc")
    role_name = unique_name("GfcRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async () => { return 1; };"
    desc = "Test description for verification"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
            Description=desc,
            Timeout=15,
            MemorySize=256,
        )
        resp = lambda_client.get_function(FunctionName=function_name)
        assert resp["Configuration"]["Description"] == desc
        assert resp["Configuration"]["Timeout"] == 15
        assert resp["Configuration"]["MemorySize"] == 256
        assert resp["Configuration"]["Runtime"] == "nodejs22.x"
        assert resp.get("Code") and resp["Code"].get("Location"), (
            "code location should not be nil"
        )
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)


def test_publish_version_verify_version(lambda_client, iam_helper, unique_name):
    function_name = unique_name("PvFunc")
    role_name = unique_name("PvRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async () => { return 1; };"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
        )
        resp = lambda_client.publish_version(FunctionName=function_name)
        assert resp["Version"] != "$LATEST"
        assert resp["Version"] == "1"
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)


def test_list_functions_returns_created(lambda_client, iam_helper, unique_name):
    function_name = unique_name("LfFunc")
    role_name = unique_name("LfRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async () => { return 1; };"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
        )
        resp = lambda_client.list_functions()
        found = False
        for f in resp["Functions"]:
            if f["FunctionName"] == function_name:
                found = True
                assert f["Runtime"] == "nodejs22.x"
                assert f["Handler"] == "index.handler"
                break
        assert found, f"created function {function_name} not found in ListFunctions"
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)


def test_create_alias_duplicate(lambda_client, iam_helper, unique_name):
    function_name = unique_name("CaFunc")
    role_name = unique_name("CaRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async () => { return 1; };"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
        )
        lambda_client.create_alias(
            FunctionName=function_name,
            Name="prod",
            FunctionVersion="$LATEST",
        )
        with pytest.raises(ClientError) as exc:
            lambda_client.create_alias(
                FunctionName=function_name,
                Name="prod",
                FunctionVersion="$LATEST",
            )
        assert_client_error(exc, "ResourceConflictException")
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)


def test_update_function_configuration_verify(lambda_client, iam_helper, unique_name):
    function_name = unique_name("UcFunc")
    role_name = unique_name("UcRole")
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
    code = b"exports.handler = async () => { return 1; };"
    iam_helper.create_role(role_name)
    try:
        lambda_client.create_function(
            FunctionName=function_name,
            Runtime="nodejs22.x",
            Role=role_arn,
            Handler="index.handler",
            Code={"ZipFile": code},
            Description="original",
        )
        lambda_client.update_function_configuration(
            FunctionName=function_name,
            Description="updated description",
            Timeout=30,
            MemorySize=512,
        )
        resp = lambda_client.get_function_configuration(FunctionName=function_name)
        assert resp["Description"] == "updated description"
        assert resp["Timeout"] == 30
        assert resp["MemorySize"] == 512
    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        iam_helper.delete_role(role_name)
