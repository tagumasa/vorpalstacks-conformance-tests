import json
import time
from ..runner import TestRunner, TestResult


async def run_sts_tests(
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
    sts_client = session.client("sts", endpoint_url=endpoint, region_name=region)
    iam_client = session.client("iam", endpoint_url=endpoint, region_name=region)

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

    def _get_caller_identity():
        resp = sts_client.get_caller_identity()
        if not resp.get("UserId"):
            raise Exception("user ID is nil")

    results.append(
        await runner.run_test("sts", "GetCallerIdentity", _get_caller_identity)
    )

    def _get_session_token():
        resp = sts_client.get_session_token()
        if not resp.get("Credentials"):
            raise Exception("credentials is nil")

    results.append(await runner.run_test("sts", "GetSessionToken", _get_session_token))

    def _assume_role():
        role_arn = f"arn:aws:iam::000000000000:role/{role_name}"
        sts_client.assume_role(
            RoleArn=role_arn,
            RoleSessionName="TestSession",
        )

    results.append(await runner.run_test("sts", "AssumeRole", _assume_role))

    def _assume_role_nonexistent():
        try:
            sts_client.assume_role(
                RoleArn="arn:aws:iam::000000000000:role/NonExistentRole",
                RoleSessionName="TestSession",
            )
            raise Exception("expected error for non-existent role")
        except Exception as e:
            if str(e) == "expected error for non-existent role":
                raise

    results.append(
        await runner.run_test(
            "sts", "AssumeRole_NonExistentRole", _assume_role_nonexistent
        )
    )

    def _get_caller_identity_content_verify():
        resp = sts_client.get_caller_identity()
        if not resp.get("Account") or resp["Account"] == "":
            raise Exception("account is nil or empty")
        if not resp.get("Arn") or resp["Arn"] == "":
            raise Exception("ARN is nil or empty")
        if not resp.get("UserId") or resp["UserId"] == "":
            raise Exception("user ID is nil or empty")

    results.append(
        await runner.run_test(
            "sts",
            "GetCallerIdentity_ContentVerify",
            _get_caller_identity_content_verify,
        )
    )

    def _get_session_token_content_verify():
        resp = sts_client.get_session_token(DurationSeconds=3600)
        creds = resp.get("Credentials", {})
        if not creds:
            raise Exception("credentials is nil")
        if not creds.get("AccessKeyId") or creds["AccessKeyId"] == "":
            raise Exception("access key ID is nil or empty")
        if not creds.get("SecretAccessKey") or creds["SecretAccessKey"] == "":
            raise Exception("secret access key is nil or empty")
        if not creds.get("SessionToken") or creds["SessionToken"] == "":
            raise Exception("session token is nil or empty")
        if not creds.get("Expiration"):
            raise Exception("expiration is zero")

    results.append(
        await runner.run_test(
            "sts", "GetSessionToken_ContentVerify", _get_session_token_content_verify
        )
    )

    try:
        iam_client.delete_role(RoleName=role_name)
    except Exception:
        pass

    return results
