import time

import pytest
from botocore.exceptions import ClientError

from conformance.conftest import assert_client_error


def test_describe_stream_nonexistent(kinesis_client):
    with pytest.raises(ClientError) as exc:
        kinesis_client.describe_stream(StreamName="NonExistentStream_xyz_12345")
    assert_client_error(exc, "ResourceNotFoundException")


def test_put_record_nonexistent(kinesis_client):
    with pytest.raises(ClientError) as exc:
        kinesis_client.put_record(
            StreamName="NonExistentStream_xyz_12345",
            Data=b"test",
            PartitionKey="key",
        )
    assert_client_error(exc, "ResourceNotFoundException")


def test_delete_stream_nonexistent(kinesis_client):
    with pytest.raises(ClientError) as exc:
        kinesis_client.delete_stream(StreamName="nonexistent-stream-xyz")
    assert_client_error(exc, "ResourceNotFoundException")


def test_create_stream_duplicate(kinesis_client, unique_name):
    dup_name = unique_name("PyDupStream")
    kinesis_client.create_stream(StreamName=dup_name, ShardCount=1)
    try:
        with pytest.raises(ClientError) as exc:
            kinesis_client.create_stream(StreamName=dup_name, ShardCount=1)
        assert_client_error(exc, "ResourceInUseException")
    finally:
        try:
            kinesis_client.delete_stream(StreamName=dup_name)
        except Exception:
            pass


def test_put_record_get_records_roundtrip(kinesis_client, unique_name):
    rt_name = unique_name("PyRTStream")
    kinesis_client.create_stream(StreamName=rt_name, ShardCount=1)
    try:
        time.sleep(1)
        test_data = b"roundtrip-kinesis-data-verify"
        put_resp = kinesis_client.put_record(
            StreamName=rt_name,
            Data=test_data,
            PartitionKey="partition-1",
        )
        assert put_resp.get("SequenceNumber"), "sequence number is nil"

        desc_resp = kinesis_client.describe_stream(StreamName=rt_name)
        shards = desc_resp["StreamDescription"]["Shards"]
        assert len(shards) > 0, "no shards"

        iter_resp = kinesis_client.get_shard_iterator(
            StreamName=rt_name,
            ShardId=shards[0]["ShardId"],
            ShardIteratorType="TRIM_HORIZON",
        )
        get_resp = kinesis_client.get_records(ShardIterator=iter_resp["ShardIterator"])
        assert len(get_resp.get("Records", [])) > 0, "no records returned"
        assert get_resp["Records"][0]["Data"] == test_data, (
            f"data mismatch: got {get_resp['Records'][0]['Data']!r}, want {test_data!r}"
        )
    finally:
        try:
            kinesis_client.delete_stream(StreamName=rt_name)
        except Exception:
            pass


def test_list_shards_nonexistent_stream(kinesis_client):
    with pytest.raises(ClientError) as exc:
        kinesis_client.list_shards(StreamName="nonexistent-stream-xyz")
    assert_client_error(exc, "ResourceNotFoundException")
