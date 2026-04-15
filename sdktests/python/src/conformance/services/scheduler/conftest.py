import pytest


@pytest.fixture(scope="module")
def scheduler_client(aws_session, endpoint, region):
    return aws_session.client("scheduler", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def iam_client(aws_session, endpoint, region):
    return aws_session.client("iam", endpoint_url=endpoint, region_name=region)
