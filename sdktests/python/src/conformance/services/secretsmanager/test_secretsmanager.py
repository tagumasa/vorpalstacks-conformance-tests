import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


@pytest.fixture(scope="module")
def secret_name(secretsmanager_client, unique_name):
    name = unique_name("TestSecret")
    secretsmanager_client.create_secret(Name=name, SecretString="my-secret-value")
    yield name
    try:
        secretsmanager_client.delete_secret(
            SecretId=name, ForceDeleteWithoutRecovery=True
        )
    except Exception:
        pass


class TestSecretLifecycle:
    def test_create_secret(self, secret_name):
        assert secret_name

    def test_describe_secret(self, secretsmanager_client, secret_name):
        resp = secretsmanager_client.describe_secret(SecretId=secret_name)
        assert resp.get("Name") is not None

    def test_get_secret_value(self, secretsmanager_client, secret_name):
        resp = secretsmanager_client.get_secret_value(SecretId=secret_name)
        assert resp.get("SecretString") or resp.get("SecretBinary")

    def test_list_secrets(self, secretsmanager_client):
        resp = secretsmanager_client.list_secrets()
        assert resp.get("SecretList") is not None

    def test_update_secret(self, secretsmanager_client, secret_name):
        secretsmanager_client.update_secret(
            SecretId=secret_name, SecretString="updated-secret-value"
        )

    def test_tag_resource(self, secretsmanager_client, secret_name):
        secretsmanager_client.tag_resource(
            SecretId=secret_name,
            Tags=[
                {"Key": "Environment", "Value": "test"},
                {"Key": "Project", "Value": "sdk-tests"},
            ],
        )

    def test_list_secret_version_ids(self, secretsmanager_client, secret_name):
        resp = secretsmanager_client.list_secret_version_ids(SecretId=secret_name)
        assert resp.get("Versions") is not None

    def test_delete_secret(self, secretsmanager_client, secret_name):
        secretsmanager_client.delete_secret(
            SecretId=secret_name, ForceDeleteWithoutRecovery=True
        )


class TestErrorCases:
    def test_get_secret_value_nonexistent(self, secretsmanager_client):
        with pytest.raises(ClientError) as exc_info:
            secretsmanager_client.get_secret_value(
                SecretId="arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz"
            )
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_describe_secret_nonexistent(self, secretsmanager_client):
        with pytest.raises(ClientError) as exc_info:
            secretsmanager_client.describe_secret(
                SecretId="arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz"
            )
        assert_client_error(exc_info, "ResourceNotFoundException")

    def test_delete_secret_nonexistent(self, secretsmanager_client):
        with pytest.raises(ClientError) as exc_info:
            secretsmanager_client.delete_secret(
                SecretId="arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-xyz",
                ForceDeleteWithoutRecovery=True,
            )
        assert_client_error(exc_info, "ResourceNotFoundException")


class TestDuplicate:
    def test_create_secret_duplicate(self, secretsmanager_client, unique_name):
        dup_name = unique_name("DupSecret")
        try:
            secretsmanager_client.create_secret(
                Name=dup_name, SecretString="initial-value"
            )
        except Exception:
            pass
        try:
            with pytest.raises(ClientError) as exc_info:
                secretsmanager_client.create_secret(
                    Name=dup_name, SecretString="duplicate-value"
                )
            assert_client_error(exc_info, "ResourceExistsException")
        finally:
            try:
                secretsmanager_client.delete_secret(
                    SecretId=dup_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass


class TestVerification:
    def test_get_secret_value_content_verify(self, secretsmanager_client, unique_name):
        verify_name = unique_name("VerifySecret")
        verify_value = "my-verified-secret-123"
        try:
            secretsmanager_client.create_secret(
                Name=verify_name, SecretString=verify_value
            )
            resp = secretsmanager_client.get_secret_value(SecretId=verify_name)
            assert resp.get("SecretString") == verify_value
        finally:
            try:
                secretsmanager_client.delete_secret(
                    SecretId=verify_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass

    def test_update_secret_content_verify(self, secretsmanager_client, unique_name):
        update_verify_name = unique_name("UpdateVerify")
        original_value = "original-value"
        updated_value = "updated-secret-value-456"
        try:
            secretsmanager_client.create_secret(
                Name=update_verify_name, SecretString=original_value
            )
            secretsmanager_client.update_secret(
                SecretId=update_verify_name, SecretString=updated_value
            )
            resp = secretsmanager_client.get_secret_value(SecretId=update_verify_name)
            assert resp.get("SecretString") == updated_value
        finally:
            try:
                secretsmanager_client.delete_secret(
                    SecretId=update_verify_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass

    def test_list_secrets_contains_created(self, secretsmanager_client, unique_name):
        list_verify_name = unique_name("ListVerify")
        try:
            secretsmanager_client.create_secret(
                Name=list_verify_name, SecretString="list-test"
            )
            resp = secretsmanager_client.list_secrets()
            found = False
            for s in resp.get("SecretList", []):
                if s.get("Name") == list_verify_name:
                    found = True
                    break
            assert found
        finally:
            try:
                secretsmanager_client.delete_secret(
                    SecretId=list_verify_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass


class TestMultiByte:
    def test_multi_byte_secret(self, secretsmanager_client, unique_name):
        ja_value = "日本語テストシークレット"
        zh_value = "简体中文测试机密"
        tw_value = "繁體中文測試機密"
        for label, value in [("ja", ja_value), ("zh", zh_value), ("tw", tw_value)]:
            name = unique_name(f"MultiByte-{label}")
            secretsmanager_client.create_secret(Name=name, SecretString=value)
            try:
                resp = secretsmanager_client.get_secret_value(SecretId=name)
                assert resp.get("SecretString") == value
            finally:
                secretsmanager_client.delete_secret(
                    SecretId=name, ForceDeleteWithoutRecovery=True
                )
