import pytest


@pytest.fixture(scope="module")
def sesv2_client(aws_session, endpoint, region):
    return aws_session.client("sesv2", endpoint_url=endpoint, region_name=region)
