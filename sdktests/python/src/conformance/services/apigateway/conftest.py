import pytest


@pytest.fixture(scope="module")
def apigateway_client(aws_session, endpoint, region):
    return aws_session.client("apigateway", endpoint_url=endpoint, region_name=region)
