import pytest


@pytest.fixture(scope="module")
def s3_client(aws_session, endpoint, region):
    return aws_session.client("s3", endpoint_url=endpoint, region_name=region)
