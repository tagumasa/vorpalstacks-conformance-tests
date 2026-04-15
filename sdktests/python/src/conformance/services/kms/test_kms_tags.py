import pytest


@pytest.fixture(scope="module")
def kms_key(kms_client):
    resp = kms_client.create_key(
        KeyUsage="ENCRYPT_DECRYPT",
        Description="Tag test key",
    )
    key_id = resp["KeyMetadata"]["KeyId"]
    yield key_id
    try:
        kms_client.schedule_key_deletion(KeyId=key_id, PendingWindowInDays=7)
    except Exception:
        pass


class TestTagResource:
    def test_tag_resource(self, kms_client, kms_key):
        kms_client.tag_resource(
            KeyId=kms_key,
            Tags=[
                {"TagKey": "Environment", "TagValue": "test"},
                {"TagKey": "Project", "TagValue": "sdk-tests"},
            ],
        )


class TestListResourceTags:
    def test_list_resource_tags(self, kms_client, kms_key):
        resp = kms_client.list_resource_tags(KeyId=kms_key)
        assert resp.get("Tags") is not None


class TestUntagResource:
    def test_untag_resource(self, kms_client, kms_key):
        kms_client.tag_resource(
            KeyId=kms_key,
            Tags=[{"TagKey": "ToRemove", "TagValue": "test"}],
        )
        kms_client.untag_resource(KeyId=kms_key, TagKeys=["ToRemove"])
