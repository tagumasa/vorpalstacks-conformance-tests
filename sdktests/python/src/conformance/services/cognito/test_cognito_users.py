import time
import pytest


@pytest.fixture(scope="module")
def user_pool(cognito_client, unique_name):
    pool_name = unique_name("PyUserPool")
    resp = cognito_client.create_user_pool(
        PoolName=pool_name,
        AutoVerifiedAttributes=["email"],
        Policies={
            "PasswordPolicy": {
                "MinimumLength": 8,
                "RequireUppercase": True,
                "RequireLowercase": True,
                "RequireNumbers": True,
                "RequireSymbols": False,
            }
        },
        Schema=[{"Name": "email", "AttributeDataType": "String", "Required": True}],
    )
    pool_id = resp["UserPool"]["Id"]
    yield pool_id
    try:
        cognito_client.delete_user_pool(UserPoolId=pool_id)
    except Exception:
        pass


@pytest.fixture(scope="module")
def user_pool_client(cognito_client, user_pool, unique_name):
    client_name = unique_name("PyUserClient")
    resp = cognito_client.create_user_pool_client(
        UserPoolId=user_pool, ClientName=client_name
    )
    yield resp["UserPoolClient"]["ClientId"]
    try:
        cognito_client.delete_user_pool_client(
            ClientId=resp["UserPoolClient"]["ClientId"], UserPoolId=user_pool
        )
    except Exception:
        pass


class TestSignUp:
    def test_sign_up(self, cognito_client, user_pool, user_pool_client):
        test_email = f"test-{int(time.time())}@example.com"
        resp = cognito_client.sign_up(
            ClientId=user_pool_client,
            Username=test_email,
            Password="TestPassword123!",
            UserAttributes=[{"Name": "email", "Value": test_email}],
        )
        assert resp.get("UserSub")


class TestConfirmSignUp:
    def test_confirm_sign_up(self, cognito_client, user_pool, user_pool_client):
        test_email = f"confirm-{int(time.time())}@example.com"
        cognito_client.sign_up(
            ClientId=user_pool_client,
            Username=test_email,
            Password="TestPassword123!",
            UserAttributes=[{"Name": "email", "Value": test_email}],
        )
        cognito_client.confirm_sign_up(
            ClientId=user_pool_client,
            Username=test_email,
            ConfirmationCode="123456",
        )


class TestAdminCreateUser:
    def test_admin_create_user(self, cognito_client, user_pool, unique_name):
        admin_email = f"admin-{int(time.time())}@example.com"
        resp = cognito_client.admin_create_user(
            UserPoolId=user_pool,
            Username=admin_email,
            UserAttributes=[{"Name": "email", "Value": admin_email}],
        )
        assert resp.get("User")

    def test_admin_create_user_verify_attributes(self, cognito_client, unique_name):
        pool_name = unique_name("PyAttrPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        try:
            attr_user = unique_name("attr-user")
            create_resp = cognito_client.admin_create_user(
                UserPoolId=pool_id,
                Username=attr_user,
                TemporaryPassword="TempPass123!",
                MessageAction="SUPPRESS",
                UserAttributes=[
                    {"Name": "email", "Value": "test@example.com"},
                    {"Name": "name", "Value": "Test User"},
                ],
            )
            assert create_resp["User"]["Username"] == attr_user
            assert create_resp["User"]["Enabled"]
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestAdminGetUser:
    def test_admin_get_user(self, cognito_client, user_pool, unique_name):
        admin_email = f"{unique_name('admin-get')}@example.com"
        cognito_client.admin_create_user(
            UserPoolId=user_pool,
            Username=admin_email,
            UserAttributes=[{"Name": "email", "Value": admin_email}],
        )
        resp = cognito_client.admin_get_user(UserPoolId=user_pool, Username=admin_email)
        assert resp.get("Username") or resp.get("User")


class TestListUsers:
    def test_list_users(self, cognito_client, user_pool):
        resp = cognito_client.list_users(UserPoolId=user_pool)
        assert resp.get("Users") is not None
        assert len(resp["Users"]) >= 1

    def test_list_users_contains_created(self, cognito_client, unique_name):
        pool_name = unique_name("PyListPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        try:
            list_user = unique_name("list-user")
            cognito_client.admin_create_user(
                UserPoolId=pool_id,
                Username=list_user,
                TemporaryPassword="TempPass123!",
                MessageAction="SUPPRESS",
            )
            list_resp = cognito_client.list_users(UserPoolId=pool_id)
            found = any(u["Username"] == list_user for u in list_resp["Users"])
            assert found, "created user not found in ListUsers"
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=pool_id)
            except Exception:
                pass


class TestAdminDisableUser:
    def test_admin_disable_user(self, cognito_client, user_pool, unique_name):
        admin_email = f"{unique_name('admin-disable')}@example.com"
        cognito_client.admin_create_user(
            UserPoolId=user_pool,
            Username=admin_email,
            UserAttributes=[{"Name": "email", "Value": admin_email}],
        )
        cognito_client.admin_disable_user(UserPoolId=user_pool, Username=admin_email)


class TestAdminEnableUser:
    def test_admin_enable_user(self, cognito_client, user_pool, unique_name):
        admin_email = f"{unique_name('admin-enable')}@example.com"
        cognito_client.admin_create_user(
            UserPoolId=user_pool,
            Username=admin_email,
            UserAttributes=[{"Name": "email", "Value": admin_email}],
        )
        cognito_client.admin_enable_user(UserPoolId=user_pool, Username=admin_email)


class TestAdminDeleteUser:
    def test_admin_delete_user(self, cognito_client, user_pool, unique_name):
        del_email = f"{unique_name('admin-del')}@example.com"
        cognito_client.admin_create_user(
            UserPoolId=user_pool,
            Username=del_email,
            UserAttributes=[{"Name": "email", "Value": del_email}],
        )
        cognito_client.admin_delete_user(UserPoolId=user_pool, Username=del_email)
