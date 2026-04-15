import pytest


@pytest.fixture(scope="module")
def kms_client(aws_session, endpoint, region):
    return aws_session.client("kms", endpoint_url=endpoint, region_name=region)
