import os
import time
import uuid

import boto3
import pytest
from botocore.exceptions import ClientError


def _make_unique_name(prefix: str) -> str:
    return f"{prefix}-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"


@pytest.fixture(scope="session")
def endpoint() -> str:
    return os.environ.get("AWS_ENDPOINT_URL", "http://localhost:8080")


@pytest.fixture(scope="session")
def region() -> str:
    return os.environ.get("AWS_REGION", "us-east-1")


@pytest.fixture(scope="session")
def aws_session():
    return boto3.Session(
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )


@pytest.fixture(scope="session")
def unique_name():
    counter = {"n": 0}

    def _gen(prefix: str = "test") -> str:
        counter["n"] += 1
        return _make_unique_name(f"{prefix}{counter['n']}")

    return _gen


def create_client(aws_session, service: str, endpoint: str, region: str):
    return aws_session.client(service, endpoint_url=endpoint, region_name=region)


def assert_client_error(exc_info, code: str | tuple[str, ...]):
    actual = exc_info.value.response["Error"]["Code"]
    expected = (code,) if isinstance(code, str) else code
    assert actual in expected, f"expected error code {expected}, got {actual}"
