import pytest


@pytest.fixture(scope="module")
def cloudfront_client(aws_session, endpoint, region):
    return aws_session.client("cloudfront", endpoint_url=endpoint, region_name=region)
