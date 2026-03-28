import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


def _create_iam_role_http(endpoint, role_name):
    import urllib.request

    trust_policy = json.dumps(
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
    form = f"Action=CreateRole&Version=2010-05-08&RoleName={role_name}&AssumeRolePolicyDocument={urllib.parse.quote(trust_policy)}"
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


def _delete_iam_role_http(endpoint, role_name):
    import urllib.request

    form = f"Action=DeleteRole&Version=2010-05-08&RoleName={role_name}"
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


async def run_lambda_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    lambda_client = session.client("lambda", endpoint_url=endpoint, region_name=region)
    iam_client = session.client("iam", endpoint_url=endpoint, region_name=region)

    function_name = _make_unique_name("PyFunc")
    role_name = _make_unique_name("PyRole")
    function_code = b"exports.handler = async (event) => { return { statusCode: 200, body: 'Hello' }; };"

    _create_iam_role_http(endpoint, role_name)
    role_arn = f"arn:aws:iam::000000000000:role/{role_name}"

    try:

        def _create_function():
            resp = lambda_client.create_function(
                FunctionName=function_name,
                Runtime="nodejs22.x",
                Role=role_arn,
                Handler="index.handler",
                Code={"ZipFile": function_code},
            )
            assert resp.get("FunctionName") == function_name

        results.append(
            await runner.run_test("lambda", "CreateFunction", _create_function)
        )

        def _get_function():
            resp = lambda_client.get_function(FunctionName=function_name)
            assert resp.get("Configuration"), "Configuration is null"

        results.append(await runner.run_test("lambda", "GetFunction", _get_function))

        def _get_function_configuration():
            resp = lambda_client.get_function_configuration(FunctionName=function_name)
            assert resp.get("FunctionName"), "function name is nil"

        results.append(
            await runner.run_test(
                "lambda", "GetFunctionConfiguration", _get_function_configuration
            )
        )

        def _list_functions():
            resp = lambda_client.list_functions()
            assert resp.get("Functions") is not None

        results.append(
            await runner.run_test("lambda", "ListFunctions", _list_functions)
        )

        def _update_function_code():
            new_code = b"exports.handler = async (event) => { return { statusCode: 200, body: 'Updated' }; };"
            resp = lambda_client.update_function_code(
                FunctionName=function_name, ZipFile=new_code
            )
            assert resp.get("LastModified"), "LastModified is nil"

        results.append(
            await runner.run_test("lambda", "UpdateFunctionCode", _update_function_code)
        )

        def _update_function_configuration():
            resp = lambda_client.update_function_configuration(
                FunctionName=function_name, Description="Updated function"
            )
            assert resp is not None

        results.append(
            await runner.run_test(
                "lambda", "UpdateFunctionConfiguration", _update_function_configuration
            )
        )

        def _publish_version():
            resp = lambda_client.publish_version(FunctionName=function_name)
            assert resp.get("Version"), "Version is nil"

        results.append(
            await runner.run_test("lambda", "PublishVersion", _publish_version)
        )

        def _list_versions_by_function():
            resp = lambda_client.list_versions_by_function(FunctionName=function_name)
            assert resp.get("Versions") is not None

        results.append(
            await runner.run_test(
                "lambda", "ListVersionsByFunction", _list_versions_by_function
            )
        )

        def _create_alias():
            resp = lambda_client.create_alias(
                FunctionName=function_name,
                Name="live",
                FunctionVersion="$LATEST",
            )
            assert resp.get("AliasArn"), "AliasArn is nil"

        results.append(await runner.run_test("lambda", "CreateAlias", _create_alias))

        def _get_alias():
            resp = lambda_client.get_alias(FunctionName=function_name, Name="live")
            assert resp.get("Name"), "alias name is nil"

        results.append(await runner.run_test("lambda", "GetAlias", _get_alias))

        def _update_alias():
            resp = lambda_client.update_alias(
                FunctionName=function_name,
                Name="live",
                Description="Production alias",
            )
            assert resp is not None

        results.append(await runner.run_test("lambda", "UpdateAlias", _update_alias))

        def _list_aliases():
            resp = lambda_client.list_aliases(FunctionName=function_name)
            assert resp.get("Aliases") is not None

        results.append(await runner.run_test("lambda", "ListAliases", _list_aliases))

        def _invoke():
            resp = lambda_client.invoke(FunctionName=function_name)
            assert resp.get("StatusCode") != 0

        results.append(await runner.run_test("lambda", "Invoke", _invoke))

        def _put_function_concurrency():
            resp = lambda_client.put_function_concurrency(
                FunctionName=function_name, ReservedConcurrentExecutions=10
            )
            assert resp is not None

        results.append(
            await runner.run_test(
                "lambda", "PutFunctionConcurrency", _put_function_concurrency
            )
        )

        def _get_function_concurrency():
            resp = lambda_client.get_function_concurrency(FunctionName=function_name)
            assert resp.get("ReservedConcurrentExecutions") is not None

        results.append(
            await runner.run_test(
                "lambda", "GetFunctionConcurrency", _get_function_concurrency
            )
        )

        def _delete_function_concurrency():
            resp = lambda_client.delete_function_concurrency(FunctionName=function_name)
            assert resp is not None

        results.append(
            await runner.run_test(
                "lambda", "DeleteFunctionConcurrency", _delete_function_concurrency
            )
        )

        def _add_permission():
            statement_id = _make_unique_name("stmt")
            resp = lambda_client.add_permission(
                FunctionName=function_name,
                StatementId=statement_id,
                Action="lambda:InvokeFunction",
                Principal="apigateway.amazonaws.com",
            )
            assert resp is not None

        results.append(
            await runner.run_test("lambda", "AddPermission", _add_permission)
        )

        def _get_policy():
            resp = lambda_client.get_policy(FunctionName=function_name)
            assert resp.get("Policy"), "policy is empty"

        results.append(await runner.run_test("lambda", "GetPolicy", _get_policy))

        def _remove_permission():
            statement_id = _make_unique_name("stmt")
            lambda_client.add_permission(
                FunctionName=function_name,
                StatementId=statement_id,
                Action="lambda:InvokeFunction",
                Principal="apigateway.amazonaws.com",
            )
            lambda_client.remove_permission(
                FunctionName=function_name, StatementId=statement_id
            )

        results.append(
            await runner.run_test("lambda", "RemovePermission", _remove_permission)
        )

        function_arn = f"arn:aws:lambda:{region}:000000000000:function:{function_name}"

        def _tag_resource():
            resp = lambda_client.tag_resource(
                Resource=function_arn,
                Tags={"Environment": "test", "Project": "sdk-tests"},
            )
            assert resp is not None

        results.append(await runner.run_test("lambda", "TagResource", _tag_resource))

        def _list_tags():
            resp = lambda_client.list_tags(Resource=function_arn)
            assert resp.get("Tags") is not None

        results.append(await runner.run_test("lambda", "ListTags", _list_tags))

        def _untag_resource():
            resp = lambda_client.untag_resource(
                Resource=function_arn, TagKeys=["Environment"]
            )
            assert resp is not None

        results.append(
            await runner.run_test("lambda", "UntagResource", _untag_resource)
        )

        def _delete_alias():
            resp = lambda_client.delete_alias(FunctionName=function_name, Name="live")
            assert resp is not None

        results.append(await runner.run_test("lambda", "DeleteAlias", _delete_alias))

        def _get_account_settings():
            resp = lambda_client.get_account_settings()
            assert resp.get("AccountLimit"), "account limit is nil"

        results.append(
            await runner.run_test("lambda", "GetAccountSettings", _get_account_settings)
        )

        def _delete_function():
            resp = lambda_client.delete_function(FunctionName=function_name)
            assert resp is not None

        results.append(
            await runner.run_test("lambda", "DeleteFunction", _delete_function)
        )

    finally:
        try:
            lambda_client.delete_function(FunctionName=function_name)
        except Exception:
            pass
        _delete_iam_role_http(endpoint, role_name)

    def _get_nonexistent():
        try:
            lambda_client.get_function(FunctionName="NoSuchFunction_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test("lambda", "GetFunction_NonExistent", _get_nonexistent)
    )

    def _invoke_nonexistent():
        try:
            lambda_client.invoke(FunctionName="NoSuchFunction_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test("lambda", "Invoke_NonExistent", _invoke_nonexistent)
    )

    def _update_function_code_nonexistent():
        try:
            lambda_client.update_function_code(
                FunctionName="NoSuchFunction_xyz_12345", ZipFile=b"code"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "lambda",
            "UpdateFunctionCode_NonExistent",
            _update_function_code_nonexistent,
        )
    )

    def _delete_function_nonexistent():
        try:
            lambda_client.delete_function(FunctionName="NoSuchFunction_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "lambda", "DeleteFunction_NonExistent", _delete_function_nonexistent
        )
    )

    def _create_function_duplicate():
        dup_name = _make_unique_name("DupFunc")
        dup_role = _make_unique_name("DupRole")
        dup_arn = f"arn:aws:iam::000000000000:role/{dup_role}"
        dup_code = b"exports.handler = async () => { return 1; };"
        _create_iam_role_http(endpoint, dup_role)
        try:
            lambda_client.create_function(
                FunctionName=dup_name,
                Runtime="nodejs22.x",
                Role=dup_arn,
                Handler="index.handler",
                Code={"ZipFile": dup_code},
            )
            try:
                lambda_client.create_function(
                    FunctionName=dup_name,
                    Runtime="nodejs22.x",
                    Role=dup_arn,
                    Handler="index.handler",
                    Code={"ZipFile": dup_code},
                )
                raise AssertionError("expected error for duplicate function name")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "ResourceConflictException"
        finally:
            try:
                lambda_client.delete_function(FunctionName=dup_name)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, dup_role)

    results.append(
        await runner.run_test(
            "lambda", "CreateFunction_DuplicateName", _create_function_duplicate
        )
    )

    def _invoke_verify_response_payload():
        inv_func = _make_unique_name("InvFunc")
        inv_role = _make_unique_name("InvRole")
        inv_arn = f"arn:aws:iam::000000000000:role/{inv_role}"
        inv_code = b'exports.handler = async (event) => { return { statusCode: 200, body: JSON.stringify({result: "ok"}) }; };'
        _create_iam_role_http(endpoint, inv_role)
        try:
            lambda_client.create_function(
                FunctionName=inv_func,
                Runtime="nodejs22.x",
                Role=inv_arn,
                Handler="index.handler",
                Code={"ZipFile": inv_code},
            )
            resp = lambda_client.invoke(FunctionName=inv_func)
            assert resp["StatusCode"] == 200
            payload = resp["Payload"].read()
            assert len(payload) > 0, "payload should not be empty"
        finally:
            try:
                lambda_client.delete_function(FunctionName=inv_func)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, inv_role)

    results.append(
        await runner.run_test(
            "lambda",
            "Invoke_VerifyResponsePayload",
            _invoke_verify_response_payload,
        )
    )

    def _get_function_contains_code_config():
        gfc_func = _make_unique_name("GfcFunc")
        gfc_role = _make_unique_name("GfcRole")
        gfc_arn = f"arn:aws:iam::000000000000:role/{gfc_role}"
        gfc_code = b"exports.handler = async () => { return 1; };"
        gfc_desc = "Test description for verification"
        _create_iam_role_http(endpoint, gfc_role)
        try:
            lambda_client.create_function(
                FunctionName=gfc_func,
                Runtime="nodejs22.x",
                Role=gfc_arn,
                Handler="index.handler",
                Code={"ZipFile": gfc_code},
                Description=gfc_desc,
                Timeout=15,
                MemorySize=256,
            )
            resp = lambda_client.get_function(FunctionName=gfc_func)
            assert resp["Configuration"]["Description"] == gfc_desc
            assert resp["Configuration"]["Timeout"] == 15
            assert resp["Configuration"]["MemorySize"] == 256
            assert resp["Configuration"]["Runtime"] == "nodejs22.x"
            assert resp.get("Code") and resp["Code"].get("Location"), (
                "code location should not be nil"
            )
        finally:
            try:
                lambda_client.delete_function(FunctionName=gfc_func)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, gfc_role)

    results.append(
        await runner.run_test(
            "lambda",
            "GetFunction_ContainsCodeConfig",
            _get_function_contains_code_config,
        )
    )

    def _publish_version_verify_version():
        pv_func = _make_unique_name("PvFunc")
        pv_role = _make_unique_name("PvRole")
        pv_arn = f"arn:aws:iam::000000000000:role/{pv_role}"
        pv_code = b"exports.handler = async () => { return 1; };"
        _create_iam_role_http(endpoint, pv_role)
        try:
            lambda_client.create_function(
                FunctionName=pv_func,
                Runtime="nodejs22.x",
                Role=pv_arn,
                Handler="index.handler",
                Code={"ZipFile": pv_code},
            )
            resp = lambda_client.publish_version(FunctionName=pv_func)
            assert resp["Version"] != "$LATEST"
            assert resp["Version"] == "1"
        finally:
            try:
                lambda_client.delete_function(FunctionName=pv_func)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, pv_role)

    results.append(
        await runner.run_test(
            "lambda",
            "PublishVersion_VerifyVersion",
            _publish_version_verify_version,
        )
    )

    def _list_functions_returns_created():
        lf_func = _make_unique_name("LfFunc")
        lf_role = _make_unique_name("LfRole")
        lf_arn = f"arn:aws:iam::000000000000:role/{lf_role}"
        lf_code = b"exports.handler = async () => { return 1; };"
        _create_iam_role_http(endpoint, lf_role)
        try:
            lambda_client.create_function(
                FunctionName=lf_func,
                Runtime="nodejs22.x",
                Role=lf_arn,
                Handler="index.handler",
                Code={"ZipFile": lf_code},
            )
            resp = lambda_client.list_functions()
            found = False
            for f in resp["Functions"]:
                if f["FunctionName"] == lf_func:
                    found = True
                    assert f["Runtime"] == "nodejs22.x"
                    assert f["Handler"] == "index.handler"
                    break
            assert found, f"created function {lf_func} not found in ListFunctions"
        finally:
            try:
                lambda_client.delete_function(FunctionName=lf_func)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, lf_role)

    results.append(
        await runner.run_test(
            "lambda",
            "ListFunctions_ReturnsCreated",
            _list_functions_returns_created,
        )
    )

    def _create_alias_duplicate():
        ca_func = _make_unique_name("CaFunc")
        ca_role = _make_unique_name("CaRole")
        ca_arn = f"arn:aws:iam::000000000000:role/{ca_role}"
        ca_code = b"exports.handler = async () => { return 1; };"
        _create_iam_role_http(endpoint, ca_role)
        try:
            lambda_client.create_function(
                FunctionName=ca_func,
                Runtime="nodejs22.x",
                Role=ca_arn,
                Handler="index.handler",
                Code={"ZipFile": ca_code},
            )
            lambda_client.create_alias(
                FunctionName=ca_func,
                Name="prod",
                FunctionVersion="$LATEST",
            )
            try:
                lambda_client.create_alias(
                    FunctionName=ca_func,
                    Name="prod",
                    FunctionVersion="$LATEST",
                )
                raise AssertionError("expected error for duplicate alias name")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "ResourceConflictException"
        finally:
            try:
                lambda_client.delete_function(FunctionName=ca_func)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, ca_role)

    results.append(
        await runner.run_test(
            "lambda",
            "CreateAlias_DuplicateName",
            _create_alias_duplicate,
        )
    )

    def _update_function_configuration_verify():
        uc_func = _make_unique_name("UcFunc")
        uc_role = _make_unique_name("UcRole")
        uc_arn = f"arn:aws:iam::000000000000:role/{uc_role}"
        uc_code = b"exports.handler = async () => { return 1; };"
        _create_iam_role_http(endpoint, uc_role)
        try:
            lambda_client.create_function(
                FunctionName=uc_func,
                Runtime="nodejs22.x",
                Role=uc_arn,
                Handler="index.handler",
                Code={"ZipFile": uc_code},
                Description="original",
            )
            lambda_client.update_function_configuration(
                FunctionName=uc_func,
                Description="updated description",
                Timeout=30,
                MemorySize=512,
            )
            resp = lambda_client.get_function_configuration(FunctionName=uc_func)
            assert resp["Description"] == "updated description"
            assert resp["Timeout"] == 30
            assert resp["MemorySize"] == 512
        finally:
            try:
                lambda_client.delete_function(FunctionName=uc_func)
            except Exception:
                pass
            _delete_iam_role_http(endpoint, uc_role)

    results.append(
        await runner.run_test(
            "lambda",
            "UpdateFunctionConfiguration_VerifyUpdate",
            _update_function_configuration_verify,
        )
    )

    return results
