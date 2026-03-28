import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_cognito_tests(
    runner: TestRunner,
    endpoint: str,
    region: str,
) -> list[TestResult]:
    results: list[TestResult] = []
    import boto3

    session = boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )
    cognito_client = session.client(
        "cognito-idp", endpoint_url=endpoint, region_name=region
    )

    user_pool_name = _make_unique_name("PyUserPool")
    domain_name = _make_unique_name("pyuserpool")
    test_email = f"test-{int(time.time())}@example.com"
    user_pool_id = ""
    user_sub = ""

    try:

        def _create_user_pool():
            nonlocal user_pool_id
            resp = cognito_client.create_user_pool(
                PoolName=user_pool_name,
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
                Schema=[
                    {"Name": "email", "AttributeDataType": "String", "Required": True}
                ],
            )
            assert resp["UserPool"]["Id"], "UserPool Id is null"
            user_pool_id = resp["UserPool"]["Id"]

        results.append(
            await runner.run_test("cognito", "CreateUserPool", _create_user_pool)
        )

        def _describe_user_pool():
            resp = cognito_client.describe_user_pool(UserPoolId=user_pool_id)
            assert resp["UserPool"], "UserPool is null"
            assert resp["UserPool"]["Id"], "UserPool Id is null"
            pool_name = resp["UserPool"].get("PoolName") or resp["UserPool"].get(
                "Name", ""
            )
            assert pool_name == user_pool_name

        results.append(
            await runner.run_test("cognito", "DescribeUserPool", _describe_user_pool)
        )

        def _list_user_pools():
            resp = cognito_client.list_user_pools(MaxResults=60)
            assert resp["UserPools"] is not None
            assert len(resp["UserPools"]) >= 1, "Expected at least one user pool"

        results.append(
            await runner.run_test("cognito", "ListUserPools", _list_user_pools)
        )

        def _create_user_pool_client():
            nonlocal client_id
            client_name = _make_unique_name("PyClient")
            resp = cognito_client.create_user_pool_client(
                UserPoolId=user_pool_id, ClientName=client_name
            )
            assert resp["UserPoolClient"]["ClientId"], "ClientId is null"
            assert resp["UserPoolClient"]["ClientName"] == client_name
            client_id = resp["UserPoolClient"]["ClientId"]

        client_id = ""

        results.append(
            await runner.run_test(
                "cognito", "CreateUserPoolClient", _create_user_pool_client
            )
        )

        def _describe_user_pool_client():
            resp = cognito_client.describe_user_pool_client(
                ClientId=client_id, UserPoolId=user_pool_id
            )
            assert resp["UserPoolClient"]["ClientId"] == client_id
            assert resp["UserPoolClient"]["UserPoolId"] == user_pool_id

        results.append(
            await runner.run_test(
                "cognito",
                "DescribeUserPoolClient",
                _describe_user_pool_client,
            )
        )

        def _update_user_pool_client():
            resp = cognito_client.update_user_pool_client(
                ClientId=client_id,
                UserPoolId=user_pool_id,
                ClientName="updated-client",
            )
            assert resp["UserPoolClient"]["ClientName"] == "updated-client"

        results.append(
            await runner.run_test(
                "cognito",
                "UpdateUserPoolClient",
                _update_user_pool_client,
            )
        )

        def _list_user_pool_clients():
            resp = cognito_client.list_user_pool_clients(
                UserPoolId=user_pool_id, MaxResults=10
            )
            assert len(resp.get("UserPoolClients", [])) > 0

        results.append(
            await runner.run_test(
                "cognito", "ListUserPoolClients", _list_user_pool_clients
            )
        )

        def _update_user_pool():
            cognito_client.update_user_pool(
                UserPoolId=user_pool_id,
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

        results.append(
            await runner.run_test("cognito", "UpdateUserPool", _update_user_pool)
        )

        def _create_user_pool_domain():
            cognito_client.create_user_pool_domain(
                Domain=domain_name, UserPoolId=user_pool_id
            )

        results.append(
            await runner.run_test(
                "cognito", "CreateUserPoolDomain", _create_user_pool_domain
            )
        )

        def _describe_user_pool_domain():
            resp = cognito_client.describe_user_pool_domain(Domain=domain_name)
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
            assert domain_desc is not None, "DomainDescription is null"
            assert domain_desc.get("UserPoolId") == user_pool_id

        results.append(
            await runner.run_test(
                "cognito", "DescribeUserPoolDomain", _describe_user_pool_domain
            )
        )

        def _delete_user_pool_domain():
            cognito_client.delete_user_pool_domain(
                Domain=domain_name, UserPoolId=user_pool_id
            )

        results.append(
            await runner.run_test(
                "cognito", "DeleteUserPoolDomain", _delete_user_pool_domain
            )
        )

        def _sign_up():
            nonlocal user_sub
            resp = cognito_client.sign_up(
                ClientId=client_id,
                Username=test_email,
                Password="TestPassword123!",
                UserAttributes=[{"Name": "email", "Value": test_email}],
            )
            assert resp.get("UserSub"), "UserSub is null"
            user_sub = resp["UserSub"]

        results.append(await runner.run_test("cognito", "SignUp", _sign_up))

        def _confirm_sign_up():
            cognito_client.confirm_sign_up(
                ClientId=client_id,
                Username=test_email,
                ConfirmationCode="123456",
            )

        results.append(
            await runner.run_test("cognito", "ConfirmSignUp", _confirm_sign_up)
        )

        def _admin_create_user():
            admin_email = f"admin-{int(time.time())}@example.com"
            resp = cognito_client.admin_create_user(
                UserPoolId=user_pool_id,
                Username=admin_email,
                UserAttributes=[{"Name": "email", "Value": admin_email}],
            )
            assert resp.get("User"), "User is null"

        results.append(
            await runner.run_test("cognito", "AdminCreateUser", _admin_create_user)
        )

        def _admin_get_user():
            admin_email = f"{_make_unique_name('admin-get')}@example.com"
            cognito_client.admin_create_user(
                UserPoolId=user_pool_id,
                Username=admin_email,
                UserAttributes=[{"Name": "email", "Value": admin_email}],
            )
            resp = cognito_client.admin_get_user(
                UserPoolId=user_pool_id, Username=admin_email
            )
            assert resp.get("Username") or resp.get("User"), "Username is null"

        results.append(
            await runner.run_test("cognito", "AdminGetUser", _admin_get_user)
        )

        def _list_users():
            resp = cognito_client.list_users(UserPoolId=user_pool_id)
            assert resp.get("Users") is not None
            assert len(resp["Users"]) >= 1, "Expected at least 1 user"

        results.append(await runner.run_test("cognito", "ListUsers", _list_users))

        def _admin_disable_user():
            admin_email = f"{_make_unique_name('admin-disable')}@example.com"
            cognito_client.admin_create_user(
                UserPoolId=user_pool_id,
                Username=admin_email,
                UserAttributes=[{"Name": "email", "Value": admin_email}],
            )
            cognito_client.admin_disable_user(
                UserPoolId=user_pool_id, Username=admin_email
            )

        results.append(
            await runner.run_test("cognito", "AdminDisableUser", _admin_disable_user)
        )

        def _admin_enable_user():
            admin_email = f"{_make_unique_name('admin-enable')}@example.com"
            cognito_client.admin_create_user(
                UserPoolId=user_pool_id,
                Username=admin_email,
                UserAttributes=[{"Name": "email", "Value": admin_email}],
            )
            cognito_client.admin_enable_user(
                UserPoolId=user_pool_id, Username=admin_email
            )

        results.append(
            await runner.run_test("cognito", "AdminEnableUser", _admin_enable_user)
        )

        def _create_group():
            nonlocal group_name
            group_name = _make_unique_name("PyGroup")
            resp = cognito_client.create_group(
                GroupName=group_name, UserPoolId=user_pool_id
            )
            assert resp["Group"]["GroupName"] == group_name

        group_name = ""

        results.append(await runner.run_test("cognito", "CreateGroup", _create_group))

        def _list_groups():
            resp = cognito_client.list_groups(UserPoolId=user_pool_id)
            assert len(resp.get("Groups", [])) > 0

        results.append(await runner.run_test("cognito", "ListGroups", _list_groups))

        def _create_resource_server():
            identifier = _make_unique_name("resource")
            resp = cognito_client.create_resource_server(
                UserPoolId=user_pool_id,
                Identifier=identifier,
                Name="Test Resource Server",
            )
            assert resp["ResourceServer"]["Identifier"] == identifier
            assert resp["ResourceServer"]["Name"] == "Test Resource Server"

        results.append(
            await runner.run_test(
                "cognito", "CreateResourceServer", _create_resource_server
            )
        )

        def _list_resource_servers():
            resp = cognito_client.list_resource_servers(UserPoolId=user_pool_id)
            assert len(resp.get("ResourceServers", [])) > 0

        results.append(
            await runner.run_test(
                "cognito", "ListResourceServers", _list_resource_servers
            )
        )

        def _create_identity_provider():
            resp = cognito_client.create_identity_provider(
                UserPoolId=user_pool_id,
                ProviderName="TestProvider",
                ProviderType="Facebook",
                ProviderDetails={
                    "client_id": "test-client-id",
                    "client_secret": "test-client-secret",
                    "authorize_scopes": "public_profile,email",
                },
            )
            assert resp["IdentityProvider"]["ProviderName"] == "TestProvider"

        results.append(
            await runner.run_test(
                "cognito", "CreateIdentityProvider", _create_identity_provider
            )
        )

        def _list_identity_providers():
            resp = cognito_client.list_identity_providers(UserPoolId=user_pool_id)
            assert len(resp.get("Providers", [])) > 0

        results.append(
            await runner.run_test(
                "cognito", "ListIdentityProviders", _list_identity_providers
            )
        )

        def _set_user_pool_mfa_config():
            cognito_client.set_user_pool_mfa_config(
                UserPoolId=user_pool_id,
                SmsMfaConfiguration={
                    "SmsConfiguration": {
                        "SnsCallerArn": "arn:aws:sns:us-east-1:123456789012:sms-topic",
                        "ExternalId": "external-id",
                    }
                },
            )

        results.append(
            await runner.run_test(
                "cognito", "SetUserPoolMfaConfig", _set_user_pool_mfa_config
            )
        )

        def _get_user_pool_mfa_config():
            resp = cognito_client.get_user_pool_mfa_config(UserPoolId=user_pool_id)
            assert (
                resp.get("MfaConfiguration")
                or resp.get("SoftwareTokenMfaConfiguration")
                or resp.get("SmsMfaConfiguration")
                or resp.get("EmailMfaConfiguration")
            ), "expected at least one MFA config field to be set"

        results.append(
            await runner.run_test(
                "cognito", "GetUserPoolMfaConfig", _get_user_pool_mfa_config
            )
        )

        def _admin_delete_user():
            del_email = f"{_make_unique_name('admin-del')}@example.com"
            cognito_client.admin_create_user(
                UserPoolId=user_pool_id,
                Username=del_email,
                UserAttributes=[{"Name": "email", "Value": del_email}],
            )
            cognito_client.admin_delete_user(
                UserPoolId=user_pool_id, Username=del_email
            )

        results.append(
            await runner.run_test("cognito", "AdminDeleteUser", _admin_delete_user)
        )

        def _delete_group():
            cognito_client.delete_group(GroupName=group_name, UserPoolId=user_pool_id)

        results.append(await runner.run_test("cognito", "DeleteGroup", _delete_group))

        def _get_csv_header():
            resp = cognito_client.get_csv_header(UserPoolId=user_pool_id)
            assert len(resp.get("CSVHeader", [])) > 0, "expected non-empty CSV header"

        results.append(
            await runner.run_test("cognito", "GetCSVHeader", _get_csv_header)
        )

        def _describe_risk_configuration():
            resp = cognito_client.describe_risk_configuration(UserPoolId=user_pool_id)
            assert resp.get("RiskConfiguration"), "RiskConfiguration is nil"

        results.append(
            await runner.run_test(
                "cognito", "DescribeRiskConfiguration", _describe_risk_configuration
            )
        )

        def _delete_user_pool_client():
            cognito_client.delete_user_pool_client(
                ClientId=client_id, UserPoolId=user_pool_id
            )

        results.append(
            await runner.run_test(
                "cognito", "DeleteUserPoolClient", _delete_user_pool_client
            )
        )

        def _delete_user_pool():
            cognito_client.delete_user_pool(UserPoolId=user_pool_id)

        results.append(
            await runner.run_test("cognito", "DeleteUserPool", _delete_user_pool)
        )

        def _tag_resource():
            nonlocal tag_pool_id, tag_pool_arn
            tag_pool_name = _make_unique_name("PyTagPool")
            resp = cognito_client.create_user_pool(PoolName=tag_pool_name)
            tag_pool_id = resp["UserPool"]["Id"]
            tag_pool_arn = resp["UserPool"]["Arn"]
            cognito_client.tag_resource(
                ResourceArn=tag_pool_arn,
                Tags={"Environment": "test", "Owner": "test-user"},
            )
            list_resp = cognito_client.list_tags_for_resource(ResourceArn=tag_pool_arn)
            assert list_resp.get("Tags"), "Tags is nil after tagging"
            assert list_resp["Tags"]["Environment"] == "test"

        tag_pool_id = ""
        tag_pool_arn = ""

        results.append(await runner.run_test("cognito", "TagResource", _tag_resource))

        def _list_tags_for_resource():
            nonlocal ltag_pool_id, ltag_pool_arn
            ltag_pool_name = _make_unique_name("PyListTagPool")
            resp = cognito_client.create_user_pool(PoolName=ltag_pool_name)
            ltag_pool_id = resp["UserPool"]["Id"]
            ltag_pool_arn = resp["UserPool"]["Arn"]
            cognito_client.tag_resource(
                ResourceArn=ltag_pool_arn, Tags={"Test": "value"}
            )
            list_resp = cognito_client.list_tags_for_resource(ResourceArn=ltag_pool_arn)
            assert list_resp.get("Tags"), "Tags is nil"
            assert list_resp["Tags"]["Test"] == "value"

        ltag_pool_id = ""
        ltag_pool_arn = ""

        results.append(
            await runner.run_test(
                "cognito", "ListTagsForResource", _list_tags_for_resource
            )
        )

        def _untag_resource():
            nonlocal utag_pool_id, utag_pool_arn
            utag_pool_name = _make_unique_name("PyUntagPool")
            resp = cognito_client.create_user_pool(PoolName=utag_pool_name)
            utag_pool_id = resp["UserPool"]["Id"]
            utag_pool_arn = resp["UserPool"]["Arn"]
            cognito_client.tag_resource(
                ResourceArn=utag_pool_arn, Tags={"Test": "value"}
            )
            cognito_client.untag_resource(ResourceArn=utag_pool_arn, TagKeys=["Test"])
            list_resp = cognito_client.list_tags_for_resource(ResourceArn=utag_pool_arn)
            assert "Test" not in list_resp.get("Tags", {}), (
                "tag Test should have been removed"
            )

        utag_pool_id = ""
        utag_pool_arn = ""

        results.append(
            await runner.run_test("cognito", "UntagResource", _untag_resource)
        )

        def _global_sign_out():
            try:
                cognito_client.global_sign_out(AccessToken="dummy-token")
                raise AssertionError("expected error for dummy access token")
            except ClientError as e:
                assert e.response["Error"]["Code"] == "NotAuthorizedException"

        results.append(
            await runner.run_test("cognito", "GlobalSignOut", _global_sign_out)
        )

    finally:
        try:
            cognito_client.delete_user_pool(UserPoolId=user_pool_id)
        except Exception:
            pass
        for pid in [tag_pool_id, ltag_pool_id, utag_pool_id]:
            if pid:
                try:
                    cognito_client.delete_user_pool(UserPoolId=pid)
                except Exception:
                    pass

    def _describe_user_pool_nonexistent():
        try:
            cognito_client.describe_user_pool(UserPoolId="nonexistent-pool-12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "ResourceNotFoundException"

    results.append(
        await runner.run_test(
            "cognito", "DescribeUserPool_NonExistent", _describe_user_pool_nonexistent
        )
    )

    def _admin_get_user_nonexistent():
        try:
            cognito_client.admin_get_user(
                UserPoolId=user_pool_id, Username="NonExistentUser_xyz_12345"
            )
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "UserNotFoundException"

    results.append(
        await runner.run_test(
            "cognito", "AdminGetUser_NonExistent", _admin_get_user_nonexistent
        )
    )

    def _delete_user_pool_nonexistent():
        try:
            cognito_client.delete_user_pool(UserPoolId="us-east-1_nonexistentpool")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            pass

    results.append(
        await runner.run_test(
            "cognito", "DeleteUserPool_NonExistent", _delete_user_pool_nonexistent
        )
    )

    def _admin_get_user_nonexistent_pool():
        err_pool = _make_unique_name("PyErrPool")
        try:
            resp = cognito_client.create_user_pool(PoolName=err_pool)
            pool_id = resp["UserPool"]["Id"]
            try:
                cognito_client.admin_get_user(
                    UserPoolId=pool_id, Username="nonexistent-user-xyz"
                )
                raise AssertionError("Expected ClientError but got none")
            except ClientError:
                pass
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=err_pool)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "cognito",
            "AdminGetUser_NonExistent",
            _admin_get_user_nonexistent_pool,
        )
    )

    def _create_user_pool_duplicate():
        dup_name = _make_unique_name("PyDupPool")
        resp1 = cognito_client.create_user_pool(PoolName=dup_name)
        try:
            resp2 = cognito_client.create_user_pool(PoolName=dup_name)
            assert resp2["UserPool"]["Id"] != resp1["UserPool"]["Id"], (
                "duplicate pool should have different ID"
            )
        except ClientError:
            pass
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=resp1["UserPool"]["Id"])
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "cognito",
            "CreateUserPool_DuplicateName",
            _create_user_pool_duplicate,
        )
    )

    def _admin_create_user_verify_attributes():
        attr_pool = _make_unique_name("PyAttrPool")
        try:
            resp = cognito_client.create_user_pool(PoolName=attr_pool)
            pool_id = resp["UserPool"]["Id"]
            attr_user = _make_unique_name("attr-user")
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
                cognito_client.delete_user_pool(UserPoolId=attr_pool)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "cognito",
            "AdminCreateUser_VerifyAttributes",
            _admin_create_user_verify_attributes,
        )
    )

    def _list_users_contains_created():
        list_pool = _make_unique_name("PyListPool")
        try:
            resp = cognito_client.create_user_pool(PoolName=list_pool)
            pool_id = resp["UserPool"]["Id"]
            list_user = _make_unique_name("list-user")
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
                cognito_client.delete_user_pool(UserPoolId=list_pool)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "cognito", "ListUsers_ContainsCreated", _list_users_contains_created
        )
    )

    def _list_groups_contains_created():
        grp_pool = _make_unique_name("PyGrpPool")
        try:
            resp = cognito_client.create_user_pool(PoolName=grp_pool)
            pool_id = resp["UserPool"]["Id"]
            test_grp = _make_unique_name("test-grp")
            cognito_client.create_group(
                GroupName=test_grp,
                UserPoolId=pool_id,
                Description="Test group description",
            )
            list_resp = cognito_client.list_groups(UserPoolId=pool_id)
            found = False
            for g in list_resp["Groups"]:
                if g["GroupName"] == test_grp:
                    found = True
                    assert g.get("Description") == "Test group description"
                    break
            assert found, "created group not found in ListGroups"
        finally:
            try:
                cognito_client.delete_user_pool(UserPoolId=grp_pool)
            except Exception:
                pass

    results.append(
        await runner.run_test(
            "cognito", "ListGroups_ContainsCreated", _list_groups_contains_created
        )
    )

    return results
