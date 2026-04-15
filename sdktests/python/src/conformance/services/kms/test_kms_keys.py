import pytest


@pytest.fixture(scope="module")
def kms_key(kms_client, unique_name):
    resp = kms_client.create_key(
        KeyUsage="ENCRYPT_DECRYPT",
        Description="Test key for SDK tests",
        Tags=[{"TagKey": "Environment", "TagValue": "Test"}],
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


@pytest.fixture(scope="module")
def kms_key_alias(kms_client, kms_key, unique_name):
    alias_name = "alias/" + unique_name("PyKey")
    kms_client.create_alias(AliasName=alias_name, TargetKeyId=kms_key)
    yield alias_name
    try:
        kms_client.delete_alias(AliasName=alias_name)
    except Exception:
        pass


class TestCreateKey:
    def test_create_key(self, kms_key):
        assert kms_key


class TestDescribeKey:
    def test_describe_key(self, kms_client, kms_key):
        resp = kms_client.describe_key(KeyId=kms_key)
        assert resp["KeyMetadata"]
        assert resp["KeyMetadata"]["KeyId"]


class TestListKeys:
    def test_list_keys(self, kms_client, kms_key):
        resp = kms_client.list_keys()
        assert resp["Keys"] is not None
        found = any(k["KeyId"] == kms_key for k in resp["Keys"])
        assert found, "Created key not found in list"


class TestUpdateKeyDescription:
    def test_update_key_description(self, kms_client, kms_key):
        kms_client.update_key_description(
            KeyId=kms_key, Description="Updated test key description"
        )


class TestEnableKey:
    def test_enable_key(self, kms_client, kms_key):
        kms_client.enable_key(KeyId=kms_key)

    def test_enable_key_after_disable(self, kms_client, kms_key):
        kms_client.disable_key(KeyId=kms_key)
        kms_client.enable_key(KeyId=kms_key)


class TestDisableKey:
    def test_disable_key(self, kms_client, kms_key):
        kms_client.disable_key(KeyId=kms_key)


class TestScheduleKeyDeletion:
    def test_schedule_key_deletion(self, kms_client, kms_key):
        resp = kms_client.schedule_key_deletion(KeyId=kms_key, PendingWindowInDays=7)
        assert resp.get("DeletionDate")


class TestCancelKeyDeletion:
    def test_cancel_key_deletion(self, kms_client, kms_key):
        kms_client.schedule_key_deletion(KeyId=kms_key, PendingWindowInDays=7)
        resp = kms_client.cancel_key_deletion(KeyId=kms_key)
        assert resp.get("KeyId")


class TestKeyRotation:
    def test_enable_key_rotation(self, kms_client, kms_key):
        kms_client.enable_key_rotation(KeyId=kms_key)

    def test_get_key_rotation_status(self, kms_client, kms_key):
        kms_client.get_key_rotation_status(KeyId=kms_key)

    def test_disable_key_rotation(self, kms_client, kms_key):
        kms_client.disable_key_rotation(KeyId=kms_key)
