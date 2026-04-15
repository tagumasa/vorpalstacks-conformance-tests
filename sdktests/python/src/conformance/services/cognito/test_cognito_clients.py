import pytest


@pytest.fixture(scope="module")
def client_pool(cognito_client, unique_name):
    pool_name = unique_name("PyClientPool")
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
def pool_client(cognito_client, client_pool, unique_name):
    client_name = unique_name("PyClient")
    resp = cognito_client.create_user_pool_client(
        UserPoolId=client_pool, ClientName=client_name
    )
    client_id = resp["UserPoolClient"]["ClientId"]
    yield client_id
    try:
        cognito_client.delete_user_pool_client(
            ClientId=client_id, UserPoolId=client_pool
        )
    except Exception:
        pass


class TestCreateUserPoolClient:
    def test_create_user_pool_client(self, cognito_client, client_pool, unique_name):
        client_name = unique_name("PyClientCreate")
        resp = cognito_client.create_user_pool_client(
            UserPoolId=client_pool, ClientName=client_name
        )
        assert resp["UserPoolClient"]["ClientId"]
        assert resp["UserPoolClient"]["ClientName"] == client_name


class TestDescribeUserPoolClient:
    def test_describe_user_pool_client(self, cognito_client, client_pool, pool_client):
        resp = cognito_client.describe_user_pool_client(
            ClientId=pool_client, UserPoolId=client_pool
        )
        assert resp["UserPoolClient"]["ClientId"] == pool_client
        assert resp["UserPoolClient"]["UserPoolId"] == client_pool


class TestUpdateUserPoolClient:
    def test_update_user_pool_client(self, cognito_client, client_pool, pool_client):
        resp = cognito_client.update_user_pool_client(
            ClientId=pool_client,
            UserPoolId=client_pool,
            ClientName="updated-client",
        )
        assert resp["UserPoolClient"]["ClientName"] == "updated-client"


class TestListUserPoolClients:
    def test_list_user_pool_clients(self, cognito_client, client_pool):
        resp = cognito_client.list_user_pool_clients(
            UserPoolId=client_pool, MaxResults=10
        )
        assert len(resp.get("UserPoolClients", [])) > 0


class TestDeleteUserPoolClient:
    def test_delete_user_pool_client(self, cognito_client, client_pool, unique_name):
        client_name = unique_name("PyClientDel")
        resp = cognito_client.create_user_pool_client(
            UserPoolId=client_pool, ClientName=client_name
        )
        client_id = resp["UserPoolClient"]["ClientId"]
        cognito_client.delete_user_pool_client(
            ClientId=client_id, UserPoolId=client_pool
        )
