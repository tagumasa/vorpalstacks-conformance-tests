import pytest


class TestCreateUser:
    def test_create_user(self, iam_resources):
        assert iam_resources["user_arn"]


class TestGetUser:
    def test_get_user(self, iam_client, iam_resources):
        resp = iam_client.get_user(UserName=iam_resources["user_name"])
        assert resp["User"]
        assert resp["User"]["Arn"]
        assert resp["User"]["UserName"] == iam_resources["user_name"]


class TestListUsers:
    def test_list_users(self, iam_client, iam_resources):
        resp = iam_client.list_users()
        assert resp["Users"] is not None
        found = any(u["UserName"] == iam_resources["user_name"] for u in resp["Users"])
        assert found, "Created user not found in list"


class TestUpdateUser:
    def test_update_user(self, iam_client, iam_resources):
        iam_client.update_user(
            UserName=iam_resources["user_name"],
            NewPath="/test-updated/",
        )


class TestTagUser:
    def test_tag_user(self, iam_client, iam_resources):
        iam_client.tag_user(
            UserName=iam_resources["user_name"],
            Tags=[{"Key": "Team", "Value": "Platform"}],
        )


class TestListTagsForUser:
    def test_list_tags_for_user(self, iam_client, iam_resources):
        iam_client.tag_user(
            UserName=iam_resources["user_name"],
            Tags=[{"Key": "ListTest", "Value": "Platform"}],
        )
        resp = iam_client.list_user_tags(UserName=iam_resources["user_name"])
        assert resp.get("Tags") is not None
        has_test = any(
            t["Key"] == "ListTest" and t["Value"] == "Platform" for t in resp["Tags"]
        )
        assert has_test, "ListTest tag not found"


class TestUntagUser:
    def test_untag_user(self, iam_client, iam_resources):
        iam_client.tag_user(
            UserName=iam_resources["user_name"],
            Tags=[{"Key": "ToUntag", "Value": "test"}],
        )
        iam_client.untag_user(UserName=iam_resources["user_name"], TagKeys=["ToUntag"])


class TestCreateAccessKey:
    def test_create_access_key(self, iam_client, iam_resources):
        resp = iam_client.create_access_key(UserName=iam_resources["user_name"])
        assert resp["AccessKey"]
        assert resp["AccessKey"]["AccessKeyId"]


class TestListAccessKeys:
    def test_list_access_keys(self, iam_client, iam_resources):
        resp = iam_client.create_access_key(UserName=iam_resources["user_name"])
        ak_id = resp["AccessKey"]["AccessKeyId"]
        try:
            list_resp = iam_client.list_access_keys(UserName=iam_resources["user_name"])
            assert list_resp["AccessKeyMetadata"] is not None
            found = any(
                k["AccessKeyId"] == ak_id for k in list_resp["AccessKeyMetadata"]
            )
            assert found, "Created access key not found in list"
        finally:
            try:
                iam_client.delete_access_key(
                    UserName=iam_resources["user_name"], AccessKeyId=ak_id
                )
            except Exception:
                pass


class TestCreateLoginProfile:
    def test_create_login_profile(self, iam_client, iam_resources):
        resp = iam_client.create_login_profile(
            UserName=iam_resources["user_name"], Password="TempPassword123!"
        )
        assert resp is not None


class TestGetLoginProfile:
    def test_get_login_profile(self, iam_client, iam_resources):
        resp = iam_client.get_login_profile(UserName=iam_resources["user_name"])
        assert resp.get("LoginProfile")


class TestDeleteLoginProfile:
    def test_delete_login_profile(self, iam_client, iam_resources):
        resp = iam_client.delete_login_profile(UserName=iam_resources["user_name"])
        assert resp is not None


class TestDeleteAccessKey:
    def test_delete_access_key(self, iam_client, iam_resources):
        resp = iam_client.create_access_key(UserName=iam_resources["user_name"])
        ak_id = resp["AccessKey"]["AccessKeyId"]
        iam_client.delete_access_key(
            UserName=iam_resources["user_name"], AccessKeyId=ak_id
        )


class TestDeleteUser:
    def test_delete_user(self, iam_client, unique_name):
        user_name = unique_name("PyDelUser")
        iam_client.create_user(UserName=user_name)
        iam_client.delete_user(UserName=user_name)
