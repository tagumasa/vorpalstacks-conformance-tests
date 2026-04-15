import pytest


@pytest.fixture(scope="module")
def acm_client(aws_session, endpoint, region):
    return aws_session.client("acm", endpoint_url=endpoint, region_name=region)
