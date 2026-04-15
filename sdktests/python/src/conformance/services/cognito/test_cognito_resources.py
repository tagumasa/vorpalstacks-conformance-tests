import pytest


@pytest.fixture(scope="module")
def resource_pool(cognito_client, unique_name):
    pool_name = unique_name("PyResPool")
    resp = cognito_client.create_user_pool(PoolName=pool_name)
    pool_id = resp["UserPool"]["Id"]
    yield pool_id
    try:
        cognito_client.delete_user_pool(UserPoolId=pool_id)
    except Exception:
        pass


class TestCreateResourceServer:
    def test_create_resource_server(self, cognito_client, resource_pool, unique_name):
        identifier = unique_name("resource")
        resp = cognito_client.create_resource_server(
            UserPoolId=resource_pool,
            Identifier=identifier,
            Name="Test Resource Server",
        )
        assert resp["ResourceServer"]["Identifier"] == identifier
        assert resp["ResourceServer"]["Name"] == "Test Resource Server"


class TestListResourceServers:
    def test_list_resource_servers(self, cognito_client, resource_pool, unique_name):
        identifier = unique_name("list-resource")
        cognito_client.create_resource_server(
            UserPoolId=resource_pool,
            Identifier=identifier,
            Name="Test Resource Server",
        )
        resp = cognito_client.list_resource_servers(UserPoolId=resource_pool)
        assert len(resp.get("ResourceServers", [])) > 0


class TestCreateIdentityProvider:
    def test_create_identity_provider(self, cognito_client, resource_pool):
        resp = cognito_client.create_identity_provider(
            UserPoolId=resource_pool,
            ProviderName="TestProvider",
            ProviderType="Facebook",
            ProviderDetails={
                "client_id": "test-client-id",
                "client_secret": "test-client-secret",
                "authorize_scopes": "public_profile,email",
            },
        )
        assert resp["IdentityProvider"]["ProviderName"] == "TestProvider"


class TestListIdentityProviders:
    def test_list_identity_providers(self, cognito_client, resource_pool):
        cognito_client.create_identity_provider(
            UserPoolId=resource_pool,
            ProviderName="ListTestProvider",
            ProviderType="Facebook",
            ProviderDetails={
                "client_id": "test-client-id",
                "client_secret": "test-client-secret",
                "authorize_scopes": "public_profile,email",
            },
        )
        resp = cognito_client.list_identity_providers(UserPoolId=resource_pool)
        assert len(resp.get("Providers", [])) > 0
