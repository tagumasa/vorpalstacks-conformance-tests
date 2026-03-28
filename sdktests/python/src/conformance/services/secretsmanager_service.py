import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_secretsmanager_tests(
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
    secrets_client = session.client(
        "secretsmanager", endpoint_url=endpoint, region_name=region
    )

    secret_name = _make_unique_name("TestSecret")
    secret_value = "my-secret-value"

    try:

        def _create_secret():
            secrets_client.create_secret(Name=secret_name, SecretString=secret_value)

        results.append(
            await runner.run_test("secretsmanager", "CreateSecret", _create_secret)
        )

        def _describe_secret():
            resp = secrets_client.describe_secret(SecretId=secret_name)
            assert resp.get("Name") is not None

        results.append(
            await runner.run_test("secretsmanager", "DescribeSecret", _describe_secret)
        )

        def _get_secret_value():
            resp = secrets_client.get_secret_value(SecretId=secret_name)
            assert resp.get("SecretString") or resp.get("SecretBinary")

        results.append(
            await runner.run_test("secretsmanager", "GetSecretValue", _get_secret_value)
        )

        def _list_secrets():
            resp = secrets_client.list_secrets()
            assert resp.get("SecretList") is not None

        results.append(
            await runner.run_test("secretsmanager", "ListSecrets", _list_secrets)
        )

        def _update_secret():
            secrets_client.update_secret(
                SecretId=secret_name, SecretString="updated-secret-value"
            )

        results.append(
            await runner.run_test("secretsmanager", "UpdateSecret", _update_secret)
        )

        def _tag_resource():
            secrets_client.tag_resource(
                SecretId=secret_name,
                Tags=[
                    {"Key": "Environment", "Value": "test"},
                    {"Key": "Project", "Value": "sdk-tests"},
                ],
            )

        results.append(
            await runner.run_test("secretsmanager", "TagResource", _tag_resource)
        )

        def _list_secret_version_ids():
            resp = secrets_client.list_secret_version_ids(SecretId=secret_name)
            assert resp.get("Versions") is not None

        results.append(
            await runner.run_test(
                "secretsmanager", "ListSecretVersionIds", _list_secret_version_ids
            )
        )

        def _delete_secret():
            secrets_client.delete_secret(
                SecretId=secret_name, ForceDeleteWithoutRecovery=True
            )

        results.append(
            await runner.run_test("secretsmanager", "DeleteSecret", _delete_secret)
        )

    finally:
        try:
            secrets_client.delete_secret(
                SecretId=secret_name, ForceDeleteWithoutRecovery=True
            )
        except Exception:
            pass

    def _get_secret_value_nonexistent():
        try:
            secrets_client.get_secret_value(
                SecretId="arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz"
            )
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "secretsmanager",
            "GetSecretValue_NonExistent",
            _get_secret_value_nonexistent,
        )
    )

    def _describe_secret_nonexistent():
        try:
            secrets_client.describe_secret(
                SecretId="arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-secret-xyz"
            )
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "secretsmanager", "DescribeSecret_NonExistent", _describe_secret_nonexistent
        )
    )

    def _delete_secret_nonexistent():
        try:
            secrets_client.delete_secret(
                SecretId="arn:aws:secretsmanager:us-east-1:000000000000:secret:nonexistent-xyz",
                ForceDeleteWithoutRecovery=True,
            )
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "secretsmanager", "DeleteSecret_NonExistent", _delete_secret_nonexistent
        )
    )

    dup_name = _make_unique_name("DupSecret")

    def _create_secret_duplicate():
        nonlocal dup_name
        try:
            secrets_client.create_secret(Name=dup_name, SecretString="initial-value")
        except Exception:
            pass

        try:
            secrets_client.create_secret(Name=dup_name, SecretString="duplicate-value")
            raise AssertionError("Expected error but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceExistsException"
        finally:
            try:
                secrets_client.delete_secret(
                    SecretId=dup_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "secretsmanager", "CreateSecret_Duplicate", _create_secret_duplicate
        )
    )

    verify_name = _make_unique_name("VerifySecret")

    def _get_secret_value_content_verify():
        nonlocal verify_name
        verify_value = "my-verified-secret-123"
        try:
            secrets_client.create_secret(Name=verify_name, SecretString=verify_value)
            resp = secrets_client.get_secret_value(SecretId=verify_name)
            assert resp.get("SecretString") == verify_value, (
                f"got {resp.get('SecretString')}, want {verify_value}"
            )
        finally:
            try:
                secrets_client.delete_secret(
                    SecretId=verify_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "secretsmanager",
            "GetSecretValue_ContentVerify",
            _get_secret_value_content_verify,
        )
    )

    update_verify_name = _make_unique_name("UpdateVerify")

    def _update_secret_content_verify():
        nonlocal update_verify_name
        original_value = "original-value"
        updated_value = "updated-secret-value-456"
        try:
            secrets_client.create_secret(
                Name=update_verify_name, SecretString=original_value
            )
            secrets_client.update_secret(
                SecretId=update_verify_name, SecretString=updated_value
            )
            resp = secrets_client.get_secret_value(SecretId=update_verify_name)
            assert resp.get("SecretString") == updated_value, (
                f"got {resp.get('SecretString')}, want {updated_value}"
            )
        finally:
            try:
                secrets_client.delete_secret(
                    SecretId=update_verify_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "secretsmanager",
            "UpdateSecret_ContentVerify",
            _update_secret_content_verify,
        )
    )

    list_verify_name = _make_unique_name("ListVerify")

    def _list_secrets_contains_created():
        nonlocal list_verify_name
        try:
            secrets_client.create_secret(
                Name=list_verify_name, SecretString="list-test"
            )
            resp = secrets_client.list_secrets()
            found = False
            for s in resp.get("SecretList", []):
                if s.get("Name") == list_verify_name:
                    found = True
                    break
            assert found, "created secret not found in ListSecrets"
        finally:
            try:
                secrets_client.delete_secret(
                    SecretId=list_verify_name, ForceDeleteWithoutRecovery=True
                )
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "secretsmanager",
            "ListSecrets_ContainsCreated",
            _list_secrets_contains_created,
        )
    )

    def _multi_byte_secret():
        ja_value = "日本語テストシークレット"
        zh_value = "简体中文测试机密"
        tw_value = "繁體中文測試機密"
        for label, value in [("ja", ja_value), ("zh", zh_value), ("tw", tw_value)]:
            name = _make_unique_name(f"MultiByte-{label}")
            secrets_client.create_secret(Name=name, SecretString=value)
            try:
                resp = secrets_client.get_secret_value(SecretId=name)
                assert resp.get("SecretString") == value, (
                    f"Mismatch for {label}: expected {value!r}, got {resp.get('SecretString')!r}"
                )
            finally:
                secrets_client.delete_secret(
                    SecretId=name, ForceDeleteWithoutRecovery=True
                )

    results.append(
        await runner.run_test("secretsmanager", "MultiByteSecret", _multi_byte_secret)
    )

    return results
