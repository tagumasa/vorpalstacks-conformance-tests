import json
import pytest


POLICY_DOCUMENT = {
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": ["s3:GetObject", "s3:PutObject"],
            "Resource": "*",
        }
    ],
}


class TestCreatePolicy:
    def test_create_policy(self, iam_resources):
        assert iam_resources["policy_arn"]


class TestGetPolicy:
    def test_get_policy(self, iam_client, iam_resources):
        resp = iam_client.get_policy(PolicyArn=iam_resources["policy_arn"])
        assert resp["Policy"]
        assert resp["Policy"]["Arn"]


class TestListPolicies:
    def test_list_policies(self, iam_client, iam_resources):
        resp = iam_client.list_policies(Scope="Local")
        assert resp["Policies"] is not None
        found = any(
            p["PolicyName"] == iam_resources["policy_name"] for p in resp["Policies"]
        )
        assert found, "Created policy not found in list"


class TestPutUserPolicy:
    def test_put_user_policy(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyUserPolicy")
        iam_client.put_user_policy(
            UserName=iam_resources["user_name"],
            PolicyName=policy_name,
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
        try:
            pass
        finally:
            try:
                iam_client.delete_user_policy(
                    UserName=iam_resources["user_name"], PolicyName=policy_name
                )
            except Exception:
                pass


class TestGetUserPolicy:
    def test_get_user_policy(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyGetUserPolicy")
        iam_client.put_user_policy(
            UserName=iam_resources["user_name"],
            PolicyName=policy_name,
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
        try:
            resp = iam_client.get_user_policy(
                UserName=iam_resources["user_name"], PolicyName=policy_name
            )
            assert resp.get("PolicyDocument")
        finally:
            try:
                iam_client.delete_user_policy(
                    UserName=iam_resources["user_name"], PolicyName=policy_name
                )
            except Exception:
                pass


class TestListUserPolicies:
    def test_list_user_policies(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyListUserPolicy")
        iam_client.put_user_policy(
            UserName=iam_resources["user_name"],
            PolicyName=policy_name,
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
        try:
            resp = iam_client.list_user_policies(UserName=iam_resources["user_name"])
            assert resp.get("PolicyNames") is not None
        finally:
            try:
                iam_client.delete_user_policy(
                    UserName=iam_resources["user_name"], PolicyName=policy_name
                )
            except Exception:
                pass


class TestDeleteUserPolicy:
    def test_delete_user_policy(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyDelUserPolicy")
        iam_client.put_user_policy(
            UserName=iam_resources["user_name"],
            PolicyName=policy_name,
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
        iam_client.delete_user_policy(
            UserName=iam_resources["user_name"], PolicyName=policy_name
        )


class TestPutRolePolicy:
    def test_put_role_policy(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyRolePolicy")
        iam_client.put_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyName=policy_name,
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
        try:
            pass
        finally:
            try:
                iam_client.delete_role_policy(
                    RoleName=iam_resources["role_name"], PolicyName=policy_name
                )
            except Exception:
                pass


class TestGetRolePolicy:
    def test_get_role_policy(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyGetRolePolicy")
        iam_client.put_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyName=policy_name,
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
        try:
            resp = iam_client.get_role_policy(
                RoleName=iam_resources["role_name"], PolicyName=policy_name
            )
            assert resp.get("PolicyDocument")
        finally:
            try:
                iam_client.delete_role_policy(
                    RoleName=iam_resources["role_name"], PolicyName=policy_name
                )
            except Exception:
                pass


class TestListRolePolicies:
    def test_list_role_policies(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyListRolePolicy")
        iam_client.put_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyName=policy_name,
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
        try:
            resp = iam_client.list_role_policies(RoleName=iam_resources["role_name"])
            assert resp.get("PolicyNames") is not None
        finally:
            try:
                iam_client.delete_role_policy(
                    RoleName=iam_resources["role_name"], PolicyName=policy_name
                )
            except Exception:
                pass


class TestDeleteRolePolicy:
    def test_delete_role_policy(self, iam_client, iam_resources, unique_name):
        policy_name = unique_name("PyDelRolePolicy")
        iam_client.put_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyName=policy_name,
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
        iam_client.delete_role_policy(
            RoleName=iam_resources["role_name"], PolicyName=policy_name
        )


class TestAttachGroupPolicy:
    def test_attach_group_policy(self, iam_client, iam_resources):
        iam_client.attach_group_policy(
            GroupName=iam_resources["group_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        try:
            pass
        finally:
            try:
                iam_client.detach_group_policy(
                    GroupName=iam_resources["group_name"],
                    PolicyArn=iam_resources["policy_arn"],
                )
            except Exception:
                pass


class TestDetachGroupPolicy:
    def test_detach_group_policy(self, iam_client, iam_resources):
        iam_client.attach_group_policy(
            GroupName=iam_resources["group_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        iam_client.detach_group_policy(
            GroupName=iam_resources["group_name"],
            PolicyArn=iam_resources["policy_arn"],
        )


class TestAttachUserPolicy:
    def test_attach_user_policy(self, iam_client, iam_resources):
        iam_client.attach_user_policy(
            UserName=iam_resources["user_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        try:
            pass
        finally:
            try:
                iam_client.detach_user_policy(
                    UserName=iam_resources["user_name"],
                    PolicyArn=iam_resources["policy_arn"],
                )
            except Exception:
                pass


class TestDetachUserPolicy:
    def test_detach_user_policy(self, iam_client, iam_resources):
        iam_client.attach_user_policy(
            UserName=iam_resources["user_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        iam_client.detach_user_policy(
            UserName=iam_resources["user_name"],
            PolicyArn=iam_resources["policy_arn"],
        )


class TestAttachRolePolicy:
    def test_attach_role_policy(self, iam_client, iam_resources):
        iam_client.attach_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        try:
            pass
        finally:
            try:
                iam_client.detach_role_policy(
                    RoleName=iam_resources["role_name"],
                    PolicyArn=iam_resources["policy_arn"],
                )
            except Exception:
                pass


class TestDetachRolePolicy:
    def test_detach_role_policy(self, iam_client, iam_resources):
        iam_client.attach_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        iam_client.detach_role_policy(
            RoleName=iam_resources["role_name"],
            PolicyArn=iam_resources["policy_arn"],
        )


class TestDeletePolicy:
    def test_delete_policy(self, iam_client, unique_name):
        policy_name = unique_name("PyDelPolicy")
        resp = iam_client.create_policy(
            PolicyName=policy_name,
            PolicyDocument=json.dumps(POLICY_DOCUMENT),
        )
        iam_client.delete_policy(PolicyArn=resp["Policy"]["Arn"])


class TestGetAccountSummary:
    def test_get_account_summary(self, iam_client):
        resp = iam_client.get_account_summary()
        assert resp.get("SummaryMap") is not None


class TestSimulatePrincipalPolicy:
    def test_simulate_principal_policy(self, iam_client, iam_resources):
        iam_client.attach_user_policy(
            UserName=iam_resources["user_name"],
            PolicyArn=iam_resources["policy_arn"],
        )
        try:
            resp = iam_client.simulate_principal_policy(
                PolicySourceArn=iam_resources["user_arn"],
                ActionNames=["s3:GetObject", "s3:PutObject"],
                ResourceArns=["*"],
            )
            eval_results = resp.get("EvaluationResults")
            assert eval_results is not None
            assert len(eval_results) == 2
        finally:
            try:
                iam_client.detach_user_policy(
                    UserName=iam_resources["user_name"],
                    PolicyArn=iam_resources["policy_arn"],
                )
            except Exception:
                pass
