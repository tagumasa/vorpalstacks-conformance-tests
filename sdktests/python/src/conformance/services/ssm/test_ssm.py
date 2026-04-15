import pytest

from conformance.conftest import assert_client_error


class TestPutParameter:
    def test_put_parameter(self, ssm_client, unique_name):
        param_name = unique_name("param")
        ssm_client.put_parameter(Name=param_name, Value="test-value", Type="String")

    def test_roundtrip(self, ssm_client, unique_name):
        roundtrip_name = unique_name("roundtrip")
        test_value = "roundtrip-test-value"
        ssm_client.put_parameter(Name=roundtrip_name, Value=test_value, Type="String")
        get_resp = ssm_client.get_parameter(Name=roundtrip_name)
        assert get_resp.get("Parameter", {}).get("Value") == test_value, (
            f"value mismatch: expected '{test_value}', got '{get_resp.get('Parameter', {}).get('Value')}'"
        )
        ssm_client.delete_parameter(Name=roundtrip_name)


class TestGetParameter:
    def test_get_parameter(self, ssm_client, unique_name):
        param_name = unique_name("param")
        ssm_client.put_parameter(Name=param_name, Value="test-value", Type="String")
        ssm_client.get_parameter(Name=param_name)

    def test_nonexistent(self, ssm_client):
        with pytest.raises(Exception):
            ssm_client.get_parameter(Name="/test/nonexistent-param-xyz")


class TestGetParameters:
    def test_get_parameters(self, ssm_client, unique_name):
        param_name = unique_name("param")
        ssm_client.put_parameter(Name=param_name, Value="test-value", Type="String")
        ssm_client.get_parameters(Names=[param_name])

    def test_invalid_names(self, ssm_client, unique_name):
        invalid_name = unique_name("invalid")
        param_name = unique_name("param")
        ssm_client.put_parameter(Name=param_name, Value="test-value", Type="String")
        resp = ssm_client.get_parameters(
            Names=[invalid_name, param_name], WithDecryption=False
        )
        if resp.get("InvalidParameters") and len(resp["InvalidParameters"]) == 0:
            raise Exception("expected invalid parameters to be reported")


class TestGetParametersByPath:
    def test_get_parameters_by_path(self, ssm_client):
        ssm_client.get_parameters_by_path(Path="/test")


class TestDescribeParameters:
    def test_describe_parameters(self, ssm_client):
        ssm_client.describe_parameters(
            ParameterFilters=[{"Key": "Path", "Values": ["/test"]}]
        )

    def test_contains_created(self, ssm_client, unique_name):
        desc_name = unique_name("desc")
        ssm_client.put_parameter(Name=desc_name, Value="describe-test", Type="String")
        desc_resp = ssm_client.describe_parameters(
            ParameterFilters=[{"Key": "Name", "Values": [desc_name]}]
        )
        found = any(p.get("Name") == desc_name for p in desc_resp.get("Parameters", []))
        assert found, "created parameter not found in describe results"
        ssm_client.delete_parameter(Name=desc_name)


class TestDeleteParameter:
    def test_delete_parameter(self, ssm_client, unique_name):
        param_name = unique_name("param")
        ssm_client.put_parameter(Name=param_name, Value="test-value", Type="String")
        ssm_client.delete_parameter(Name=param_name)

    def test_nonexistent(self, ssm_client):
        with pytest.raises(Exception):
            ssm_client.delete_parameter(Name="/test/nonexistent-param-xyz")


class TestMultiByteParameter:
    def test_multi_byte_parameter(self, ssm_client, unique_name):
        ja_value = "日本語テストパラメータ"
        zh_value = "简体中文测试参数"
        tw_value = "繁體中文測試參數"
        for label, value in [("ja", ja_value), ("zh", zh_value), ("tw", tw_value)]:
            name = unique_name(f"multibyte-{label}")
            ssm_client.put_parameter(Name=name, Value=value, Type="String")
            resp = ssm_client.get_parameter(Name=name)
            assert resp["Parameter"]["Value"] == value, (
                f"Mismatch for {label}: expected {value!r}, got {resp['Parameter']['Value']!r}"
            )
            ssm_client.delete_parameter(Name=name)
