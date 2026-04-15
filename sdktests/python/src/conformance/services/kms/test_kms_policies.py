import json
import pytest


@pytest.fixture(scope="module")
def kms_key(kms_client):
    resp = kms_client.create_key(
        KeyUsage="ENCRYPT_DECRYPT",
        Description="Policy test key",
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


class TestGetKeyPolicy:
    def test_get_key_policy(self, kms_client, kms_key):
        resp = kms_client.get_key_policy(KeyId=kms_key, PolicyName="default")
        assert resp.get("Policy")

    def test_get_key_policy_content_verify(self, kms_client):
        resp = kms_client.create_key(Description="Policy verify")
        try:
            policy = json.dumps(
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Principal": {"AWS": "*"},
                            "Action": "kms:*",
                            "Resource": "*",
                        }
                    ],
                }
            )
            kms_client.put_key_policy(
                KeyId=resp["KeyMetadata"]["KeyId"],
                PolicyName="default",
                Policy=policy,
            )
            get_resp = kms_client.get_key_policy(
                KeyId=resp["KeyMetadata"]["KeyId"], PolicyName="default"
            )
            assert get_resp["Policy"] == policy
        finally:
            try:
                kms_client.schedule_key_deletion(
                    KeyId=resp["KeyMetadata"]["KeyId"], PendingWindowInDays=7
                )
            except Exception:
                pass


class TestPutKeyPolicy:
    def test_put_key_policy(self, kms_client, kms_key):
        policy = json.dumps(
            {
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Principal": {"AWS": "*"},
                        "Action": "kms:*",
                        "Resource": "*",
                    }
                ],
            }
        )
        kms_client.put_key_policy(KeyId=kms_key, PolicyName="default", Policy=policy)


class TestListKeyPolicies:
    def test_list_key_policies(self, kms_client, kms_key):
        resp = kms_client.list_key_policies(KeyId=kms_key)
        assert resp.get("PolicyNames") is not None
