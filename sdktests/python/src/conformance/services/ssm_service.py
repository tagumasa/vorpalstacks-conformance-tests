import time
from ..runner import TestRunner, TestResult


async def run_ssm_tests(
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
    client = session.client("ssm", endpoint_url=endpoint, region_name=region)

    param_name = f"/test/param-{int(time.time() * 1000)}"

    def _put_parameter():
        client.put_parameter(
            Name=param_name,
            Value="test-value",
            Type="String",
        )

    results.append(await runner.run_test("ssm", "PutParameter", _put_parameter))

    def _get_parameter():
        client.get_parameter(Name=param_name)

    results.append(await runner.run_test("ssm", "GetParameter", _get_parameter))

    def _get_parameters():
        client.get_parameters(Names=[param_name])

    results.append(await runner.run_test("ssm", "GetParameters", _get_parameters))

    def _get_parameters_by_path():
        client.get_parameters_by_path(Path="/test")

    results.append(
        await runner.run_test("ssm", "GetParametersByPath", _get_parameters_by_path)
    )

    def _describe_parameters():
        client.describe_parameters(
            ParameterFilters=[
                {
                    "Key": "Path",
                    "Values": ["/test"],
                }
            ]
        )

    results.append(
        await runner.run_test("ssm", "DescribeParameters", _describe_parameters)
    )

    def _delete_parameter():
        client.delete_parameter(Name=param_name)

    results.append(await runner.run_test("ssm", "DeleteParameter", _delete_parameter))

    def _get_parameter_nonexistent():
        try:
            client.get_parameter(Name="/test/nonexistent-param-xyz")
            raise Exception("expected error for non-existent parameter")
        except Exception as e:
            if str(e) == "expected error for non-existent parameter":
                raise

    results.append(
        await runner.run_test(
            "ssm", "GetParameter_NonExistent", _get_parameter_nonexistent
        )
    )

    def _delete_parameter_nonexistent():
        try:
            client.delete_parameter(Name="/test/nonexistent-param-xyz")
            raise Exception("expected error for non-existent parameter")
        except Exception as e:
            if str(e) == "expected error for non-existent parameter":
                raise

    results.append(
        await runner.run_test(
            "ssm", "DeleteParameter_NonExistent", _delete_parameter_nonexistent
        )
    )

    def _put_parameter_get_parameter_roundtrip():
        roundtrip_name = f"/test/roundtrip-{int(time.time() * 1000)}"
        test_value = "roundtrip-test-value"
        client.put_parameter(
            Name=roundtrip_name,
            Value=test_value,
            Type="String",
        )
        get_resp = client.get_parameter(Name=roundtrip_name)
        if get_resp.get("Parameter", {}).get("Value") != test_value:
            raise Exception(
                f"value mismatch: expected '{test_value}', got '{get_resp.get('Parameter', {}).get('Value')}'"
            )
        client.delete_parameter(Name=roundtrip_name)

    results.append(
        await runner.run_test(
            "ssm",
            "PutParameter_GetParameter_Roundtrip",
            _put_parameter_get_parameter_roundtrip,
        )
    )

    invalid_name = f"/test/invalid-{int(time.time() * 1000)}"

    def _get_parameters_invalid_names():
        resp = client.get_parameters(
            Names=[invalid_name, param_name],
            WithDecryption=False,
        )
        if resp.get("InvalidParameters") and len(resp["InvalidParameters"]) == 0:
            raise Exception("expected invalid parameters to be reported")

    results.append(
        await runner.run_test(
            "ssm", "GetParameters_InvalidNames", _get_parameters_invalid_names
        )
    )

    def _describe_parameters_contains_created():
        desc_name = f"/test/desc-{int(time.time() * 1000)}"
        client.put_parameter(
            Name=desc_name,
            Value="describe-test",
            Type="String",
        )
        desc_resp = client.describe_parameters(
            ParameterFilters=[
                {
                    "Key": "Name",
                    "Values": [desc_name],
                }
            ]
        )
        found = any(p.get("Name") == desc_name for p in desc_resp.get("Parameters", []))
        if not found:
            raise Exception("created parameter not found in describe results")
        client.delete_parameter(Name=desc_name)

    results.append(
        await runner.run_test(
            "ssm",
            "DescribeParameters_ContainsCreated",
            _describe_parameters_contains_created,
        )
    )

    def _multi_byte_parameter():
        ja_value = "日本語テストパラメータ"
        zh_value = "简体中文测试参数"
        tw_value = "繁體中文測試參數"
        for label, value in [("ja", ja_value), ("zh", zh_value), ("tw", tw_value)]:
            name = f"/test/multibyte-{label}-{int(time.time() * 1000)}"
            client.put_parameter(Name=name, Value=value, Type="String")
            resp = client.get_parameter(Name=name)
            assert resp["Parameter"]["Value"] == value, (
                f"Mismatch for {label}: expected {value!r}, got {resp['Parameter']['Value']!r}"
            )
            client.delete_parameter(Name=name)

    results.append(
        await runner.run_test("ssm", "MultiByteParameter", _multi_byte_parameter)
    )

    return results
