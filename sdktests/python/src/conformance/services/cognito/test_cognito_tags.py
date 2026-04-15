import pytest


class TestTagResource:
    def test_tag_resource(self, cognito_client, unique_name):
        pool_name = unique_name("PyTagPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        pool_arn = resp["UserPool"]["Arn"]
        try:
            cognito_client.tag_resource(
                ResourceArn=pool_arn,
                Tags={"Environment": "test", "Owner": "test-user"},
            )
            list_resp = cognito_client.list_tags_for_resource(ResourceArn=pool_arn)
            assert list_resp.get("Tags")
            assert list_resp["Tags"]["Environment"] == "test"
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestListTagsForResource:
    def test_list_tags_for_resource(self, cognito_client, unique_name):
        pool_name = unique_name("PyListTagPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        pool_arn = resp["UserPool"]["Arn"]
        try:
            cognito_client.tag_resource(ResourceArn=pool_arn, Tags={"Test": "value"})
            list_resp = cognito_client.list_tags_for_resource(ResourceArn=pool_arn)
            assert list_resp.get("Tags")
            assert list_resp["Tags"]["Test"] == "value"
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestUntagResource:
    def test_untag_resource(self, cognito_client, unique_name):
        pool_name = unique_name("PyUntagPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        pool_arn = resp["UserPool"]["Arn"]
        try:
            cognito_client.tag_resource(ResourceArn=pool_arn, Tags={"Test": "value"})
            cognito_client.untag_resource(ResourceArn=pool_arn, TagKeys=["Test"])
            list_resp = cognito_client.list_tags_for_resource(ResourceArn=pool_arn)
            assert "Test" not in list_resp.get("Tags", {})
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass
