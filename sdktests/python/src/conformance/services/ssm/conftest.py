import pytest


@pytest.fixture(scope="module")
def ssm_client(aws_session, endpoint, region):
    return aws_session.client("ssm", endpoint_url=endpoint, region_name=region)
