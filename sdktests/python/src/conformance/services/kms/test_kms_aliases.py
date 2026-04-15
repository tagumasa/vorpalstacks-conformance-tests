import pytest
from botocore.exceptions import ClientError
from conformance.conftest import assert_client_error


@pytest.fixture(scope="module")
def kms_key(kms_client):
    resp = kms_client.create_key(
        KeyUsage="ENCRYPT_DECRYPT",
        Description="Alias test key",
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


@pytest.fixture(scope="module")
def kms_alias(kms_client, kms_key, unique_name):
    alias_name = "alias/" + unique_name("PyKey")
    kms_client.create_alias(AliasName=alias_name, TargetKeyId=kms_key)
    yield alias_name
    try:
        kms_client.delete_alias(AliasName=alias_name)
    except Exception:
        pass


class TestCreateAlias:
    def test_create_alias(self, kms_client, kms_key, unique_name):
        alias_name = "alias/" + unique_name("PyKey")
        kms_client.create_alias(AliasName=alias_name, TargetKeyId=kms_key)
        try:
            pass
        finally:
            try:
                kms_client.delete_alias(AliasName=alias_name)
            except Exception:
                pass


class TestListAliases:
    def test_list_aliases(self, kms_client):
        resp = kms_client.list_aliases()
        assert resp.get("Aliases") is not None

    def test_list_aliases_contains_created(self, kms_client, kms_key, unique_name):
        alias_name = "alias/" + unique_name("LaAlias")
        kms_client.create_alias(AliasName=alias_name, TargetKeyId=kms_key)
        try:
            list_resp = kms_client.list_aliases()
            found = any(a["AliasName"] == alias_name for a in list_resp["Aliases"])
            assert found, f"Created alias {alias_name} not found in ListAliases"
        finally:
            try:
                kms_client.delete_alias(AliasName=alias_name)
            except Exception:
                pass


class TestUpdateAlias:
    def test_update_alias(self, kms_client, kms_key, kms_alias):
        kms_client.update_alias(AliasName=kms_alias, TargetKeyId=kms_key)
        list_resp = kms_client.list_aliases()
        found = any(a["AliasName"] == kms_alias for a in list_resp["Aliases"])
        assert found, f"alias {kms_alias} not found after update"


class TestDeleteAlias:
    def test_delete_alias(self, kms_client, kms_key, unique_name):
        alias_name = "alias/" + unique_name("PyKeyDel")
        kms_client.create_alias(AliasName=alias_name, TargetKeyId=kms_key)
        kms_client.delete_alias(AliasName=alias_name)


class TestCreateAliasDuplicate:
    def test_create_alias_duplicate(self, kms_client, kms_key, unique_name):
        dup_alias = "alias/" + unique_name("DupAlias")
        kms_client.create_alias(AliasName=dup_alias, TargetKeyId=kms_key)
        try:
            with pytest.raises(ClientError) as exc:
                kms_client.create_alias(AliasName=dup_alias, TargetKeyId=kms_key)
            assert_client_error(exc, "AlreadyExistsException")
        finally:
            try:
                kms_client.delete_alias(AliasName=dup_alias)
            except Exception:
                pass
