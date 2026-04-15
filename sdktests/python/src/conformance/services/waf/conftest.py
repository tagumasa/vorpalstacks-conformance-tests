import pytest


@pytest.fixture(scope="module")
def waf_client(aws_session, endpoint, region):
    return aws_session.client("wafv2", endpoint_url=endpoint, region_name=region)
