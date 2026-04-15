import pytest
from conformance.conftest import assert_client_error


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
    yield {"id": pool_id, "name": pool_name}
    try:
        cognito_client.delete_user_pool(UserPoolId=pool_id)
    except Exception:
        pass


@pytest.fixture(scope="module")
def user_pool_client(cognito_client, user_pool):
    client_name = f"{user_pool['name']}-client"
    resp = cognito_client.create_user_pool_client(
        UserPoolId=user_pool["id"], ClientName=client_name
    )
    client_id = resp["UserPoolClient"]["ClientId"]
    yield client_id
    try:
        cognito_client.delete_user_pool_client(
            ClientId=client_id, UserPoolId=user_pool["id"]
        )
    except Exception:
        pass


@pytest.fixture(scope="module")
def user_pool_group(cognito_client, user_pool, unique_name):
    group_name = unique_name("PyGroup")
    resp = cognito_client.create_group(GroupName=group_name, UserPoolId=user_pool["id"])
    yield group_name
    try:
        cognito_client.delete_group(GroupName=group_name, UserPoolId=user_pool["id"])
    except Exception:
        pass


class TestCreateUserPool:
    def test_create_user_pool(self, user_pool):
        assert user_pool["id"]


class TestDescribeUserPool:
    def test_describe_user_pool(self, cognito_client, user_pool):
        resp = cognito_client.describe_user_pool(UserPoolId=user_pool["id"])
        assert resp["UserPool"]
        assert resp["UserPool"]["Id"]
        pool_name = resp["UserPool"].get("PoolName") or resp["UserPool"].get("Name", "")
        assert pool_name == user_pool["name"]


class TestListUserPools:
    def test_list_user_pools(self, cognito_client):
        resp = cognito_client.list_user_pools(MaxResults=60)
        assert resp["UserPools"] is not None
        assert len(resp["UserPools"]) >= 1


class TestUpdateUserPool:
    def test_update_user_pool(self, cognito_client, user_pool):
        cognito_client.update_user_pool(
            UserPoolId=user_pool["id"],
            Policies={
                "PasswordPolicy": {
                    "MinimumLength": 12,
                    "RequireUppercase": True,
                    "RequireLowercase": True,
                    "RequireNumbers": True,
                    "RequireSymbols": True,
                }
            },
        )


class TestDeleteUserPool:
    def test_delete_user_pool(self, cognito_client, unique_name):
        pool_name = unique_name("PyDelPool")
        resp = cognito_client.create_user_pool(PoolName=pool_name)
        pool_id = resp["UserPool"]["Id"]
        cognito_client.delete_user_pool(UserPoolId=pool_id)


class TestUserPoolDomain:
    @pytest.fixture(scope="class")
    def domain(self, cognito_client, user_pool, unique_name):
        domain_name = unique_name("pyuserpool")
        cognito_client.create_user_pool_domain(
            Domain=domain_name, UserPoolId=user_pool["id"]
        )
        yield domain_name
        try:
            cognito_client.delete_user_pool_domain(
                Domain=domain_name, UserPoolId=user_pool["id"]
            )
        except Exception:
            pass

    def test_create_user_pool_domain(self, cognito_client, user_pool, domain):
        pass

    def test_describe_user_pool_domain(self, cognito_client, user_pool, domain):
        resp = cognito_client.describe_user_pool_domain(Domain=domain)
        domain_desc = (
            resp.get("DomainDescription")
            or resp.get("domainDescription")
            or resp.get("domain_description")
        )
        if not domain_desc:
            for key in resp:
                val = resp[key]
                if isinstance(val, dict) and val.get("UserPoolId"):
                    domain_desc = val
                    break
        assert domain_desc is not None
        assert domain_desc.get("UserPoolId") == user_pool["id"]

    def test_delete_user_pool_domain(self, cognito_client, user_pool, domain):
        cognito_client.delete_user_pool_domain(
            Domain=domain, UserPoolId=user_pool["id"]
        )


class TestGetCSVHeader:
    def test_get_csv_header(self, cognito_client, user_pool):
        resp = cognito_client.get_csv_header(UserPoolId=user_pool["id"])
        assert len(resp.get("CSVHeader", [])) > 0


class TestDescribeRiskConfiguration:
    def test_describe_risk_configuration(self, cognito_client, user_pool):
        resp = cognito_client.describe_risk_configuration(UserPoolId=user_pool["id"])
        assert resp.get("RiskConfiguration")


class TestMfaConfig:
    def test_set_user_pool_mfa_config(self, cognito_client, user_pool):
        cognito_client.set_user_pool_mfa_config(
            UserPoolId=user_pool["id"],
            SmsMfaConfiguration={
                "SmsConfiguration": {
                    "SnsCallerArn": "arn:aws:sns:us-east-1:123456789012:sms-topic",
                    "ExternalId": "external-id",
                }
            },
        )

    def test_get_user_pool_mfa_config(self, cognito_client, user_pool):
        resp = cognito_client.get_user_pool_mfa_config(UserPoolId=user_pool["id"])
        assert (
            resp.get("MfaConfiguration")
            or resp.get("SoftwareTokenMfaConfiguration")
            or resp.get("SmsMfaConfiguration")
            or resp.get("EmailMfaConfiguration")
        )


class TestGlobalSignOut:
    def test_global_sign_out_invalid_token(self, cognito_client):
        with pytest.raises(Exception):
            cognito_client.global_sign_out(AccessToken="dummy-token")
