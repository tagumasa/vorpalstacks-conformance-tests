import pytest


@pytest.fixture(scope="module")
def cloudtrail_client(aws_session, endpoint, region):
    return aws_session.client("cloudtrail", endpoint_url=endpoint, region_name=region)
