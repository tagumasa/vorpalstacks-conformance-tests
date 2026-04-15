import time

import pytest


@pytest.fixture(scope="module")
def kinesis_client(aws_session, endpoint, region):
    return aws_session.client("kinesis", endpoint_url=endpoint, region_name=region)


@pytest.fixture(scope="module")
def kinesis_stream_setup(kinesis_client, unique_name, region):
    stream_name = unique_name("PyStream")
    kinesis_client.create_stream(StreamName=stream_name, ShardCount=1)
    for _ in range(30):
        resp = kinesis_client.describe_stream(StreamName=stream_name)
        if resp["StreamDescription"]["StreamStatus"] == "ACTIVE":
            break
        time.sleep(1)
    stream_arn = resp["StreamDescription"]["StreamARN"]
    yield {"stream_name": stream_name, "stream_arn": stream_arn}
    try:
        kinesis_client.delete_stream(StreamName=stream_name)
    except Exception:
        pass
