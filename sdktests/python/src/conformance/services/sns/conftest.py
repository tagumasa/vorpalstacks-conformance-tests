import pytest


@pytest.fixture(scope="module")
def sns_client(aws_session, endpoint, region):
    return aws_session.client("sns", endpoint_url=endpoint, region_name=region)
