import pytest


@pytest.fixture(scope="module")
def route53_client(aws_session, endpoint, region):
    return aws_session.client("route53", endpoint_url=endpoint, region_name=region)
