import pytest


@pytest.fixture(scope="module")
def timestream_client(aws_session, endpoint, region):
    return aws_session.client("timestream-write", endpoint_url=endpoint, region_name=region)
