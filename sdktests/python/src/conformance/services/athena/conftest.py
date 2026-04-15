import pytest


@pytest.fixture(scope="module")
def athena_client(aws_session, endpoint, region):
    return aws_session.client("athena", endpoint_url=endpoint, region_name=region)
