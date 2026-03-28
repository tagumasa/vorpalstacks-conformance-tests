import json
import time
import uuid
from botocore.exceptions import ClientError
from ..runner import TestRunner, TestResult


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


async def run_iam_tests(
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
    iam_client = session.client("iam", endpoint_url=endpoint, region_name=region)

    user_name = _make_unique_name("PyUser")
    group_name = _make_unique_name("PyGroup")
    role_name = _make_unique_name("PyRole")
    policy_name = _make_unique_name("PyPolicy")
    user_arn = ""
    group_arn = ""
    role_arn = ""
    policy_arn = ""
    access_key_id = ""

    trust_policy = {
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Principal": {"Service": ["lambda.amazonaws.com", "ec2.amazonaws.com"]},
                "Action": "sts:AssumeRole",
            }
        ],
    }

    policy_document = {
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Action": ["s3:GetObject", "s3:PutObject"],
                "Resource": "*",
            }
        ],
    }

    try:

        def _create_user():
            nonlocal user_arn
            resp = iam_client.create_user(
                UserName=user_name,
                Path="/test/",
                Tags=[{"Key": "Environment", "Value": "Test"}],
            )
            assert resp["User"]["Arn"], "User Arn is null"
            user_arn = resp["User"]["Arn"]

        results.append(await runner.run_test("iam", "CreateUser", _create_user))

        def _get_user():
            resp = iam_client.get_user(UserName=user_name)
            assert resp["User"], "User is null"
            assert resp["User"]["Arn"], "User Arn is null"
            assert resp["User"]["UserName"] == user_name

        results.append(await runner.run_test("iam", "GetUser", _get_user))

        def _list_users():
            resp = iam_client.list_users()
            assert resp["Users"] is not None
            found = any(u["UserName"] == user_name for u in resp["Users"])
            assert found, "Created user not found in list"

        results.append(await runner.run_test("iam", "ListUsers", _list_users))

        def _update_user():
            nonlocal user_name
            new_name = user_name + "_updated"
            iam_client.update_user(
                UserName=user_name,
                NewPath="/test-updated/",
                NewUserName=new_name,
            )
            user_name = new_name

        results.append(await runner.run_test("iam", "UpdateUser", _update_user))

        def _tag_user():
            iam_client.tag_user(
                UserName=user_name, Tags=[{"Key": "Team", "Value": "Platform"}]
            )

        results.append(await runner.run_test("iam", "TagUser", _tag_user))

        def _list_tags_for_user():
            resp = iam_client.list_user_tags(UserName=user_name)
            assert resp.get("Tags") is not None
            has_team = any(
                t["Key"] == "Team" and t["Value"] == "Platform" for t in resp["Tags"]
            )
            assert has_team, "Team tag not found"

        results.append(
            await runner.run_test("iam", "ListTagsForUser", _list_tags_for_user)
        )

        def _untag_user():
            iam_client.untag_user(UserName=user_name, TagKeys=["Team"])

        results.append(await runner.run_test("iam", "UntagUser", _untag_user))

        def _create_access_key():
            nonlocal access_key_id
            resp = iam_client.create_access_key(UserName=user_name)
            assert resp["AccessKey"], "AccessKey is null"
            assert resp["AccessKey"]["AccessKeyId"], "AccessKeyId is null"
            access_key_id = resp["AccessKey"]["AccessKeyId"]

        results.append(
            await runner.run_test("iam", "CreateAccessKey", _create_access_key)
        )

        def _list_access_keys():
            resp = iam_client.list_access_keys(UserName=user_name)
            assert resp["AccessKeyMetadata"] is not None
            found = any(
                k["AccessKeyId"] == access_key_id for k in resp["AccessKeyMetadata"]
            )
            assert found, "Created access key not found in list"

        results.append(
            await runner.run_test("iam", "ListAccessKeys", _list_access_keys)
        )

        def _create_login_profile():
            resp = iam_client.create_login_profile(
                UserName=user_name, Password="TempPassword123!"
            )
            assert resp is not None

        results.append(
            await runner.run_test("iam", "CreateLoginProfile", _create_login_profile)
        )

        def _get_login_profile():
            resp = iam_client.get_login_profile(UserName=user_name)
            assert resp.get("LoginProfile"), "login profile is nil"

        results.append(
            await runner.run_test("iam", "GetLoginProfile", _get_login_profile)
        )

        def _delete_login_profile():
            resp = iam_client.delete_login_profile(UserName=user_name)
            assert resp is not None

        results.append(
            await runner.run_test("iam", "DeleteLoginProfile", _delete_login_profile)
        )

        def _delete_access_key():
            iam_client.delete_access_key(UserName=user_name, AccessKeyId=access_key_id)

        results.append(
            await runner.run_test("iam", "DeleteAccessKey", _delete_access_key)
        )

        def _create_group():
            nonlocal group_arn
            resp = iam_client.create_group(GroupName=group_name, Path="/test/")
            assert resp["Group"]["Arn"], "Group Arn is null"
            group_arn = resp["Group"]["Arn"]

        results.append(await runner.run_test("iam", "CreateGroup", _create_group))

        def _get_group():
            resp = iam_client.get_group(GroupName=group_name)
            assert resp["Group"], "Group is null"
            assert resp["Group"]["Arn"], "Group Arn is null"

        results.append(await runner.run_test("iam", "GetGroup", _get_group))

        def _list_groups():
            resp = iam_client.list_groups()
            assert resp["Groups"] is not None
            found = any(g["GroupName"] == group_name for g in resp["Groups"])
            assert found, "Created group not found in list"

        results.append(await runner.run_test("iam", "ListGroups", _list_groups))

        def _add_user_to_group():
            iam_client.add_user_to_group(GroupName=group_name, UserName=user_name)

        results.append(
            await runner.run_test("iam", "AddUserToGroup", _add_user_to_group)
        )

        def _list_groups_for_user():
            resp = iam_client.list_groups_for_user(UserName=user_name)
            assert resp["Groups"] is not None
            found = any(g["GroupName"] == group_name for g in resp["Groups"])
            assert found, "User not in expected group"

        results.append(
            await runner.run_test("iam", "ListGroupsForUser", _list_groups_for_user)
        )

        def _remove_user_from_group():
            iam_client.remove_user_from_group(GroupName=group_name, UserName=user_name)

        results.append(
            await runner.run_test("iam", "RemoveUserFromGroup", _remove_user_from_group)
        )

        def _create_role():
            nonlocal role_arn
            resp = iam_client.create_role(
                RoleName=role_name,
                AssumeRolePolicyDocument=json.dumps(trust_policy),
                Path="/test/",
                Description="Test role for SDK tests",
            )
            assert resp["Role"]["Arn"], "Role Arn is null"
            role_arn = resp["Role"]["Arn"]

        results.append(await runner.run_test("iam", "CreateRole", _create_role))

        def _create_role_invalid_name():
            try:
                iam_client.create_role(
                    RoleName="invalid:role-name",
                    AssumeRolePolicyDocument=json.dumps(trust_policy),
                )
                raise AssertionError("expected error for invalid role name with colon")
            except ClientError:
                pass

        results.append(
            await runner.run_test(
                "iam", "CreateRole_InvalidName", _create_role_invalid_name
            )
        )

        def _get_role():
            resp = iam_client.get_role(RoleName=role_name)
            assert resp["Role"], "Role is null"
            assert resp["Role"]["Arn"], "Role Arn is null"
            assert resp["Role"]["AssumeRolePolicyDocument"], (
                "AssumeRolePolicyDocument is null"
            )

        results.append(await runner.run_test("iam", "GetRole", _get_role))

        def _list_roles():
            resp = iam_client.list_roles()
            assert resp["Roles"] is not None
            found = any(r["RoleName"] == role_name for r in resp["Roles"])
            assert found, "Created role not found in list"

        results.append(await runner.run_test("iam", "ListRoles", _list_roles))

        def _update_role_description():
            iam_client.update_role(RoleName=role_name, Description="Updated test role")

        results.append(
            await runner.run_test(
                "iam", "UpdateRoleDescription", _update_role_description
            )
        )

        def _tag_role():
            iam_client.tag_role(
                RoleName=role_name,
                Tags=[{"Key": "Environment", "Value": "test"}],
            )

        results.append(await runner.run_test("iam", "TagRole", _tag_role))

        def _list_role_tags():
            resp = iam_client.list_role_tags(RoleName=role_name)
            assert resp.get("Tags") is not None

        results.append(await runner.run_test("iam", "ListRoleTags", _list_role_tags))

        def _untag_role():
            iam_client.untag_role(RoleName=role_name, TagKeys=["Environment"])

        results.append(await runner.run_test("iam", "UntagRole", _untag_role))

        def _create_policy():
            nonlocal policy_arn
            resp = iam_client.create_policy(
                PolicyName=policy_name,
                PolicyDocument=json.dumps(policy_document),
                Description="Test policy for SDK tests",
            )
            assert resp["Policy"]["Arn"], "Policy Arn is null"
            policy_arn = resp["Policy"]["Arn"]

        results.append(await runner.run_test("iam", "CreatePolicy", _create_policy))

        def _get_policy():
            resp = iam_client.get_policy(PolicyArn=policy_arn)
            assert resp["Policy"], "Policy is null"
            assert resp["Policy"]["Arn"], "Policy Arn is null"

        results.append(await runner.run_test("iam", "GetPolicy", _get_policy))

        def _list_policies():
            resp = iam_client.list_policies(Scope="Local")
            assert resp["Policies"] is not None
            found = any(p["PolicyName"] == policy_name for p in resp["Policies"])
            assert found, "Created policy not found in list"

        results.append(await runner.run_test("iam", "ListPolicies", _list_policies))

        profile_name = _make_unique_name("PyProfile")

        def _create_instance_profile():
            resp = iam_client.create_instance_profile(InstanceProfileName=profile_name)
            assert resp.get("InstanceProfile"), "instance profile is nil"

        results.append(
            await runner.run_test(
                "iam", "CreateInstanceProfile", _create_instance_profile
            )
        )

        def _get_instance_profile():
            resp = iam_client.get_instance_profile(InstanceProfileName=profile_name)
            assert resp.get("InstanceProfile"), "instance profile is nil"

        results.append(
            await runner.run_test("iam", "GetInstanceProfile", _get_instance_profile)
        )

        def _list_instance_profiles():
            resp = iam_client.list_instance_profiles()
            assert resp.get("InstanceProfiles") is not None

        results.append(
            await runner.run_test(
                "iam", "ListInstanceProfiles", _list_instance_profiles
            )
        )

        def _add_role_to_instance_profile():
            iam_client.add_role_to_instance_profile(
                InstanceProfileName=profile_name, RoleName=role_name
            )

        results.append(
            await runner.run_test(
                "iam", "AddRoleToInstanceProfile", _add_role_to_instance_profile
            )
        )

        def _remove_role_from_instance_profile():
            iam_client.remove_role_from_instance_profile(
                InstanceProfileName=profile_name, RoleName=role_name
            )

        results.append(
            await runner.run_test(
                "iam",
                "RemoveRoleFromInstanceProfile",
                _remove_role_from_instance_profile,
            )
        )

        def _delete_instance_profile():
            iam_client.delete_instance_profile(InstanceProfileName=profile_name)

        results.append(
            await runner.run_test(
                "iam", "DeleteInstanceProfile", _delete_instance_profile
            )
        )

        user_inline_policy = _make_unique_name("PyUserPolicy")
        role_inline_policy = _make_unique_name("PyRolePolicy")

        def _put_user_policy():
            iam_client.put_user_policy(
                UserName=user_name,
                PolicyName=user_inline_policy,
                PolicyDocument=json.dumps(
                    {
                        "Version": "2012-10-17",
                        "Statement": [
                            {
                                "Effect": "Allow",
                                "Action": "s3:GetObject",
                                "Resource": "*",
                            }
                        ],
                    }
                ),
            )

        results.append(await runner.run_test("iam", "PutUserPolicy", _put_user_policy))

        def _get_user_policy():
            resp = iam_client.get_user_policy(
                UserName=user_name, PolicyName=user_inline_policy
            )
            assert resp.get("PolicyDocument"), "policy document is empty"

        results.append(await runner.run_test("iam", "GetUserPolicy", _get_user_policy))

        def _list_user_policies():
            resp = iam_client.list_user_policies(UserName=user_name)
            assert resp.get("PolicyNames") is not None

        results.append(
            await runner.run_test("iam", "ListUserPolicies", _list_user_policies)
        )

        def _delete_user_policy():
            iam_client.delete_user_policy(
                UserName=user_name, PolicyName=user_inline_policy
            )

        results.append(
            await runner.run_test("iam", "DeleteUserPolicy", _delete_user_policy)
        )

        def _put_role_policy():
            iam_client.put_role_policy(
                RoleName=role_name,
                PolicyName=role_inline_policy,
                PolicyDocument=json.dumps(
                    {
                        "Version": "2012-10-17",
                        "Statement": [
                            {
                                "Effect": "Allow",
                                "Action": "logs:*",
                                "Resource": "*",
                            }
                        ],
                    }
                ),
            )

        results.append(await runner.run_test("iam", "PutRolePolicy", _put_role_policy))

        def _get_role_policy():
            resp = iam_client.get_role_policy(
                RoleName=role_name, PolicyName=role_inline_policy
            )
            assert resp.get("PolicyDocument"), "policy document is empty"

        results.append(await runner.run_test("iam", "GetRolePolicy", _get_role_policy))

        def _list_role_policies():
            resp = iam_client.list_role_policies(RoleName=role_name)
            assert resp.get("PolicyNames") is not None

        results.append(
            await runner.run_test("iam", "ListRolePolicies", _list_role_policies)
        )

        def _get_account_summary():
            resp = iam_client.get_account_summary()
            assert resp.get("SummaryMap") is not None

        results.append(
            await runner.run_test("iam", "GetAccountSummary", _get_account_summary)
        )

        def _delete_role_policy():
            iam_client.delete_role_policy(
                RoleName=role_name, PolicyName=role_inline_policy
            )

        results.append(
            await runner.run_test("iam", "DeleteRolePolicy", _delete_role_policy)
        )

        def _attach_group_policy():
            iam_client.attach_group_policy(GroupName=group_name, PolicyArn=policy_arn)

        results.append(
            await runner.run_test("iam", "AttachGroupPolicy", _attach_group_policy)
        )

        def _detach_group_policy():
            iam_client.detach_group_policy(GroupName=group_name, PolicyArn=policy_arn)

        results.append(
            await runner.run_test("iam", "DetachGroupPolicy", _detach_group_policy)
        )

        def _delete_group():
            iam_client.delete_group(GroupName=group_name)

        results.append(await runner.run_test("iam", "DeleteGroup", _delete_group))

        def _attach_user_policy():
            iam_client.attach_user_policy(UserName=user_name, PolicyArn=policy_arn)

        results.append(
            await runner.run_test("iam", "AttachUserPolicy", _attach_user_policy)
        )

        def _detach_user_policy():
            iam_client.detach_user_policy(UserName=user_name, PolicyArn=policy_arn)

        results.append(
            await runner.run_test("iam", "DetachUserPolicy", _detach_user_policy)
        )

        def _attach_role_policy():
            iam_client.attach_role_policy(RoleName=role_name, PolicyArn=policy_arn)

        results.append(
            await runner.run_test("iam", "AttachRolePolicy", _attach_role_policy)
        )

        def _detach_role_policy():
            iam_client.detach_role_policy(RoleName=role_name, PolicyArn=policy_arn)

        results.append(
            await runner.run_test("iam", "DetachRolePolicy", _detach_role_policy)
        )

        def _simulate_principal_policy():
            resp = iam_client.simulate_principal_policy(
                PolicySourceArn=user_arn,
                ActionNames=["s3:GetObject", "s3:PutObject"],
                ResourceArns=["*"],
            )
            eval_results = resp.get("EvaluationResults")
            assert eval_results is not None, "EvaluationResults is null"
            assert len(eval_results) == 2, (
                f"Expected 2 results, got {len(eval_results)}"
            )

        results.append(
            await runner.run_test(
                "iam", "SimulatePrincipalPolicy", _simulate_principal_policy
            )
        )

        def _delete_role():
            iam_client.delete_role(RoleName=role_name)

        results.append(await runner.run_test("iam", "DeleteRole", _delete_role))

        def _delete_policy():
            iam_client.delete_policy(PolicyArn=policy_arn)

        results.append(await runner.run_test("iam", "DeletePolicy", _delete_policy))

        def _delete_user():
            iam_client.delete_user(UserName=user_name)

        results.append(await runner.run_test("iam", "DeleteUser", _delete_user))

    finally:
        try:
            iam_client.delete_access_key(UserName=user_name, AccessKeyId=access_key_id)
        except Exception:
            pass
        try:
            iam_client.remove_user_from_group(GroupName=group_name, UserName=user_name)
        except Exception:
            pass
        try:
            iam_client.delete_group(GroupName=group_name)
        except Exception:
            pass
        try:
            iam_client.delete_role(RoleName=role_name)
        except Exception:
            pass
        try:
            iam_client.delete_user(UserName=user_name)
        except Exception:
            pass

    def _get_user_nonexistent():
        try:
            iam_client.get_user(UserName="NonExistentUser_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NoSuchEntity"

    results.append(
        await runner.run_test("iam", "GetUser_NonExistent", _get_user_nonexistent)
    )

    def _get_group_nonexistent():
        try:
            iam_client.get_group(GroupName="NonExistentGroup_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NoSuchEntity"

    results.append(
        await runner.run_test("iam", "GetGroup_NonExistent", _get_group_nonexistent)
    )

    def _get_role_nonexistent():
        try:
            iam_client.get_role(RoleName="NonExistentRole_xyz_12345")
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "NoSuchEntity"

    results.append(
        await runner.run_test("iam", "GetRole_NonExistent", _get_role_nonexistent)
    )

    def _create_user_duplicate():
        dup_name = _make_unique_name("PyDupUser")
        iam_client.create_user(UserName=dup_name)
        try:
            iam_client.create_user(UserName=dup_name)
            raise AssertionError("Expected ClientError but got none")
        except ClientError as e:
            assert e.response["Error"]["Code"] == "EntityAlreadyExists"
        finally:
            try:
                iam_client.delete_user(UserName=dup_name)
            except Exception:
                pass

    results.append(
        await runner.run_test("iam", "CreateUser_Duplicate", _create_user_duplicate)
    )

    return results
