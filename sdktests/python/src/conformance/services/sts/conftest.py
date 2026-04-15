import json
import time

import pytest


@pytest.fixture(scope="module")
def sts_client(aws_session, endpoint, region):
    return aws_session.client("sts", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def iam_client(aws_session, endpoint, region):
    return aws_session.client("iam", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def sts_role(iam_client):
    role_name = f"TestRole-{int(time.time() * 1000) % 1000000}"
    trust_policy = {
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Principal": {"AWS": "arn:aws:iam::000000000000:root"},
                "Action": "sts:AssumeRole",
            }
        ],
    }
    try:
        iam_client.create_role(
            RoleName=role_name,
            AssumeRolePolicyDocument=json.dumps(trust_policy),
        )
    except Exception:
        pass
    yield role_name
    try:
        iam_client.delete_role(RoleName=role_name)
    except Exception:
        pass
