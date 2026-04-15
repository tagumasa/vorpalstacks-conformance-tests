import pytest


@pytest.fixture(scope="module")
def cognito_client(aws_session, endpoint, region):
    return aws_session.client("cognito-idp", endpoint_url=endpoint, region_name=region)
