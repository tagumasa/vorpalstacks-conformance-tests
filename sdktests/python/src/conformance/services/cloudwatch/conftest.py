import pytest


@pytest.fixture(scope="module")
def cloudwatch_client(aws_session, endpoint, region):
    return aws_session.client("cloudwatch", endpoint_url=endpoint, region_name=region)
