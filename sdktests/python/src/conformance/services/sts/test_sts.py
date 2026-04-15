import pytest


class TestGetCallerIdentity:
    def test_get_caller_identity(self, sts_client):
        resp = sts_client.get_caller_identity()
        assert resp.get("UserId"), "user ID is nil"

    def test_content_verify(self, sts_client):
        resp = sts_client.get_caller_identity()
        assert resp.get("Account") and resp["Account"] != "", "account is nil or empty"
        assert resp.get("Arn") and resp["Arn"] != "", "ARN is nil or empty"
        assert resp.get("UserId") and resp["UserId"] != "", "user ID is nil or empty"


class TestGetSessionToken:
    def test_get_session_token(self, sts_client):
        resp = sts_client.get_session_token()
        assert resp.get("Credentials"), "credentials is nil"

    def test_content_verify(self, sts_client):
        resp = sts_client.get_session_token(DurationSeconds=3600)
        creds = resp.get("Credentials", {})
        assert creds, "credentials is nil"
        assert creds.get("AccessKeyId") and creds["AccessKeyId"] != "", (
            "access key ID is nil or empty"
        )
        assert creds.get("SecretAccessKey") and creds["SecretAccessKey"] != "", (
            "secret access key is nil or empty"
        )
        assert creds.get("SessionToken") and creds["SessionToken"] != "", (
            "session token is nil or empty"
        )
        assert creds.get("Expiration"), "expiration is zero"


class TestAssumeRole:
    def test_assume_role(self, sts_client, sts_role):
        role_arn = f"arn:aws:iam::000000000000:role/{sts_role}"
        sts_client.assume_role(
            RoleArn=role_arn,
            RoleSessionName="TestSession",
        )

    def test_nonexistent_role(self, sts_client):
        with pytest.raises(Exception):
            sts_client.assume_role(
                RoleArn="arn:aws:iam::000000000000:role/NonExistentRole",
                RoleSessionName="TestSession",
            )
