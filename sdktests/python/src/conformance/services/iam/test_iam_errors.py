import pytest
from botocore.exceptions import ClientError
from conformance.conftest import assert_client_error


class TestGetUserNonExistent:
    def test_get_user_nonexistent(self, iam_client):
        with pytest.raises(ClientError) as exc:
            iam_client.get_user(UserName="NonExistentUser_xyz_12345")
        assert_client_error(exc, "NoSuchEntity")


class TestGetGroupNonExistent:
    def test_get_group_nonexistent(self, iam_client):
        with pytest.raises(ClientError) as exc:
            iam_client.get_group(GroupName="NonExistentGroup_xyz_12345")
        assert_client_error(exc, "NoSuchEntity")


class TestGetRoleNonExistent:
    def test_get_role_nonexistent(self, iam_client):
        with pytest.raises(ClientError) as exc:
            iam_client.get_role(RoleName="NonExistentRole_xyz_12345")
        assert_client_error(exc, "NoSuchEntity")


class TestCreateUserDuplicate:
    def test_create_user_duplicate(self, iam_client, unique_name):
        dup_name = unique_name("PyDupUser")
        iam_client.create_user(UserName=dup_name)
        try:
            with pytest.raises(ClientError) as exc:
                iam_client.create_user(UserName=dup_name)
            assert_client_error(exc, "EntityAlreadyExists")
        finally:
            try:
                iam_client.delete_user(UserName=dup_name)
            except Exception:
                pass


class TestCreateRoleInvalidName:
    def test_create_role_invalid_name(self, iam_client):
        import json

        trust_policy = {
            "Version": "2012-10-17",
            "Statement": [
                {
                    "Effect": "Allow",
                    "Principal": {"Service": ["lambda.amazonaws.com"]},
                    "Action": "sts:AssumeRole",
                }
            ],
        }
        with pytest.raises(ClientError):
            iam_client.create_role(
                RoleName="invalid:role-name",
                AssumeRolePolicyDocument=json.dumps(trust_policy),
            )
