import pytest


@pytest.fixture(scope="module")
def sqs_client(aws_session, endpoint, region):
    return aws_session.client("sqs", endpoint_url=endpoint, region_name=region)
