import pytest


@pytest.fixture(scope="module")
def dynamodb_client(aws_session, endpoint, region):
    return aws_session.client("dynamodb", endpoint_url=endpoint, region_name=region)
