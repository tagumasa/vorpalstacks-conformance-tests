import pytest


@pytest.fixture(scope="module")
def cloudwatchlogs_client(aws_session, endpoint, region):
    return aws_session.client("logs", endpoint_url=endpoint, region_name=region)
