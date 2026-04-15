import pytest


@pytest.fixture(scope="module")
def secretsmanager_client(aws_session, endpoint, region):
    return aws_session.client("secretsmanager", endpoint_url=endpoint, region_name=region)
