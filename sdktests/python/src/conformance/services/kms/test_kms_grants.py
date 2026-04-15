import pytest


@pytest.fixture(scope="module")
def kms_key(kms_client):
    resp = kms_client.create_key(
        KeyUsage="ENCRYPT_DECRYPT",
        Description="Grant test key",
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


@pytest.fixture(scope="module")
def kms_grant(kms_client, kms_key):
    resp = kms_client.create_grant(
        KeyId=kms_key,
        GranteePrincipal="arn:aws:iam::000000000000:user/test",
        Operations=["Encrypt", "Decrypt", "GenerateDataKey"],
    )
    grant_id = resp["GrantId"]
    grant_token = resp.get("GrantToken")
    yield {"id": grant_id, "token": grant_token}


class TestCreateGrant:
    def test_create_grant(self, kms_client, kms_key):
        resp = kms_client.create_grant(
            KeyId=kms_key,
            GranteePrincipal="arn:aws:iam::000000000000:user/test",
            Operations=["Encrypt", "Decrypt"],
        )
        assert resp["GrantId"]


class TestListGrants:
    def test_list_grants(self, kms_client, kms_key, kms_grant):
        resp = kms_client.list_grants(KeyId=kms_key)
        assert resp["Grants"] is not None
        found = any(g["GrantId"] == kms_grant["id"] for g in resp["Grants"])
        assert found, "Created grant not found in list"


class TestListRetirableGrants:
    def test_list_retirable_grants(self, kms_client):
        resp = kms_client.list_retirable_grants(
            RetiringPrincipal="arn:aws:iam::000000000000:user/TestUser"
        )
        assert resp["Grants"] is not None


class TestRetireGrant:
    def test_retire_grant(self, kms_client, kms_key):
        retire_resp = kms_client.create_grant(
            KeyId=kms_key,
            GranteePrincipal="arn:aws:iam::000000000000:user/test",
            Operations=["Encrypt", "Decrypt"],
        )
        kms_client.retire_grant(GrantToken=retire_resp["GrantToken"])


class TestRevokeGrant:
    def test_revoke_grant(self, kms_client, kms_key, kms_grant):
        kms_client.revoke_grant(KeyId=kms_key, GrantId=kms_grant["id"])
