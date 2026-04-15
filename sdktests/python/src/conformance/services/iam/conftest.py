import json

import pytest


@pytest.fixture(scope="module")
def iam_client(aws_session, endpoint, region):
    return aws_session.client("iam", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def iam_resources(iam_client, unique_name):
    user_name = unique_name("PyUser")
    resp = iam_client.create_user(
        UserName=user_name,
        Path="/test/",
        Tags=[{"Key": "Environment", "Value": "Test"}],
    )
    user_arn = resp["User"]["Arn"]

    group_name = unique_name("PyGroup")
    resp = iam_client.create_group(GroupName=group_name, Path="/test/")
    group_arn = resp["Group"]["Arn"]

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

    role_name = unique_name("PyRole")
    resp = iam_client.create_role(
        RoleName=role_name,
        AssumeRolePolicyDocument=json.dumps(trust_policy),
        Path="/test/",
        Description="Test role for SDK tests",
    )
    role_arn = resp["Role"]["Arn"]

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

    policy_name = unique_name("PyPolicy")
    resp = iam_client.create_policy(
        PolicyName=policy_name,
        PolicyDocument=json.dumps(policy_document),
        Description="Test policy for SDK tests",
    )
    policy_arn = resp["Policy"]["Arn"]

    yield {
        "user_name": user_name,
        "user_arn": user_arn,
        "group_name": group_name,
        "group_arn": group_arn,
        "role_name": role_name,
        "role_arn": role_arn,
        "policy_name": policy_name,
        "policy_arn": policy_arn,
    }
    try:
        iam_client.delete_policy(PolicyArn=policy_arn)
    except Exception:
        pass
    try:
        iam_client.detach_role_policy(RoleName=role_name, PolicyArn=policy_arn)
    except Exception:
        pass
    try:
        iam_client.detach_user_policy(UserName=user_name, PolicyArn=policy_arn)
    except Exception:
        pass
    try:
        iam_client.detach_group_policy(GroupName=group_name, PolicyArn=policy_arn)
    except Exception:
        pass
    try:
        iam_client.remove_user_from_group(GroupName=group_name, UserName=user_name)
    except Exception:
        pass
    try:
        iam_client.delete_role(RoleName=role_name)
    except Exception:
        pass
    try:
        iam_client.delete_group(GroupName=group_name)
    except Exception:
        pass
    try:
        iam_client.delete_user(UserName=user_name)
    except Exception:
        pass
