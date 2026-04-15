import json
import pytest
from botocore.exceptions import ClientError


TRUST_POLICY = {
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {"Service": ["lambda.amazonaws.com", "ec2.amazonaws.com"]},
            "Action": "sts:AssumeRole",
        }
    ],
}


class TestCreateRole:
    def test_create_role(self, iam_resources):
        assert iam_resources["role_arn"]


class TestGetRole:
    def test_get_role(self, iam_client, iam_resources):
        resp = iam_client.get_role(RoleName=iam_resources["role_name"])
        assert resp["Role"]
        assert resp["Role"]["Arn"]
        assert resp["Role"]["AssumeRolePolicyDocument"]


class TestListRoles:
    def test_list_roles(self, iam_client, iam_resources):
        resp = iam_client.list_roles()
        assert resp["Roles"] is not None
        found = any(r["RoleName"] == iam_resources["role_name"] for r in resp["Roles"])
        assert found, "Created role not found in list"


class TestUpdateRoleDescription:
    def test_update_role_description(self, iam_client, iam_resources):
        iam_client.update_role(
            RoleName=iam_resources["role_name"], Description="Updated test role"
        )


class TestTagRole:
    def test_tag_role(self, iam_client, iam_resources):
        iam_client.tag_role(
            RoleName=iam_resources["role_name"],
            Tags=[{"Key": "Environment", "Value": "test"}],
        )


class TestListRoleTags:
    def test_list_role_tags(self, iam_client, iam_resources):
        resp = iam_client.list_role_tags(RoleName=iam_resources["role_name"])
        assert resp.get("Tags") is not None


class TestUntagRole:
    def test_untag_role(self, iam_client, iam_resources):
        iam_client.tag_role(
            RoleName=iam_resources["role_name"],
            Tags=[{"Key": "ToUntagRole", "Value": "test"}],
        )
        iam_client.untag_role(
            RoleName=iam_resources["role_name"], TagKeys=["ToUntagRole"]
        )


class TestDeleteRole:
    def test_delete_role(self, iam_client, unique_name):
        role_name = unique_name("PyDelRole")
        iam_client.create_role(
            RoleName=role_name,
            AssumeRolePolicyDocument=json.dumps(TRUST_POLICY),
        )
        iam_client.delete_role(RoleName=role_name)
