import pytest
from botocore.exceptions import ClientError
from conformance.conftest import assert_client_error


class TestDescribeKeyNonExistent:
    def test_describe_key_nonexistent(self, kms_client):
        with pytest.raises(ClientError) as exc:
            kms_client.describe_key(KeyId="12345678-1234-1234-1234-123456789012")
        assert_client_error(exc, ("NotFoundException", "DependencyTimeoutException"))


class TestEncryptNonExistent:
    def test_encrypt_nonexistent(self, kms_client):
        with pytest.raises(ClientError) as exc:
            kms_client.encrypt(
                KeyId="12345678-1234-1234-1234-123456789012",
                Plaintext=b"test",
            )
        assert_client_error(exc, ("NotFoundException", "DependencyTimeoutException"))


class TestDecryptNonExistent:
    def test_decrypt_nonexistent(self, kms_client):
        with pytest.raises(ClientError) as exc:
            kms_client.decrypt(CiphertextBlob=b"invalid ciphertext")
        assert_client_error(
            exc, ("InvalidCiphertextException", "NotFoundException", "InternalFailure")
        )


class TestEncryptDisabledKey:
    def test_encrypt_disabled_key(self, kms_client):
        dis_resp = kms_client.create_key(Description="Disable test")
        try:
            dis_key_id = dis_resp["KeyMetadata"]["KeyId"]
            kms_client.disable_key(KeyId=dis_key_id)
            try:
                with pytest.raises(ClientError) as exc:
                    kms_client.encrypt(KeyId=dis_key_id, Plaintext=b"should fail")
                assert_client_error(exc, "DisabledException")
            finally:
                try:
                    kms_client.enable_key(KeyId=dis_key_id)
                except Exception:
                    pass
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=dis_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass


class TestScheduleKeyDeletionInvalidWindow:
    def test_schedule_key_deletion_invalid_window(self, kms_client):
        inv_resp = kms_client.create_key(Description="Invalid window test")
        try:
            with pytest.raises(ClientError):
                kms_client.schedule_key_deletion(
                    KeyId=inv_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=3
                )
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=inv_resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass
