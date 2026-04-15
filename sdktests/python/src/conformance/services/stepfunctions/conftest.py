import pytest


@pytest.fixture(scope="module")
def stepfunctions_client(aws_session, endpoint, region):
    return aws_session.client(
        "stepfunctions", endpoint_url=endpoint, region_name=region
    )
