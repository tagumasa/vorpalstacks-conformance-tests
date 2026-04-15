import pytest
from botocore.exceptions import ClientError
from conformance.conftest import assert_client_error


class TestDescribeUserPoolNonExistent:
    def test_describe_user_pool_nonexistent(self, cognito_client):
        with pytest.raises(ClientError) as exc:
            cognito_client.describe_user_pool(UserPoolId="nonexistent-pool-12345")
        assert_client_error(exc, "ResourceNotFoundException")


class TestAdminGetUserNonExistent:
    def test_admin_get_user_nonexistent(self, cognito_client, unique_name):
        pool_name = unique_name("PyErrPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        try:
            with pytest.raises(ClientError):
                cognito_client.admin_get_user(
                    UserPoolId=pool_id, Username="nonexistent-user-xyz"
                )
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestAdminGetUserNonExistentInPool:
    def test_admin_get_user_nonexistent_in_pool(self, cognito_client, unique_name):
        pool_name = unique_name("PyErrPool2")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        try:
            with pytest.raises(ClientError):
                cognito_client.admin_get_user(
                    UserPoolId=pool_id, Username="NonExistentUser_xyz_12345"
                )
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestDeleteUserPoolNonExistent:
    def test_delete_user_pool_nonexistent(self, cognito_client):
        with pytest.raises(ClientError):
            cognito_client.delete_user_pool(UserPoolId="us-east-1_nonexistentpool")
