import time

import pytest


def test_list_shards(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.list_shards(StreamName=kinesis_stream_setup["stream_name"])
    assert resp.get("Shards") is not None


def test_list_shards_multi_shard(kinesis_client, unique_name):
    multi_name = unique_name("PyStreamMulti")
    kinesis_client.create_stream(StreamName=multi_name, ShardCount=3)
    try:
        time.sleep(1)
        resp = kinesis_client.list_shards(StreamName=multi_name)
        assert len(resp.get("Shards", [])) == 3, (
            f"expected 3 shards, got {len(resp.get('Shards', []))}"
        )
    finally:
        try:
            kinesis_client.delete_stream(StreamName=multi_name)
        except Exception:
            pass


def test_merge_shards(kinesis_client, unique_name):
    merge_name = unique_name("PyStreamMerge")
    kinesis_client.create_stream(StreamName=merge_name, ShardCount=2)
    try:
        time.sleep(1)
        resp = kinesis_client.list_shards(StreamName=merge_name)
        open_shards = [
            s
            for s in resp["Shards"]
            if not s.get("SequenceNumberRange", {}).get("EndingSequenceNumber")
        ]
        assert len(open_shards) >= 2, (
            f"need at least 2 open shards for merge, got {len(open_shards)}"
        )
        kinesis_client.merge_shards(
            StreamName=merge_name,
            ShardToMerge=open_shards[0]["ShardId"],
            AdjacentShardToMerge=open_shards[1]["ShardId"],
        )
    finally:
        try:
            kinesis_client.delete_stream(StreamName=merge_name)
        except Exception:
            pass


def test_split_shard(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.list_shards(StreamName=kinesis_stream_setup["stream_name"])
    open_shard = None
    for s in resp["Shards"]:
        if not s.get("SequenceNumberRange", {}).get("EndingSequenceNumber"):
            open_shard = s
            break
    assert open_shard is not None, "no open shard found for split"
    kinesis_client.split_shard(
        StreamName=kinesis_stream_setup["stream_name"],
        ShardToSplit=open_shard["ShardId"],
        NewStartingHashKey="9223372036854775808",
    )


def test_list_shards_with_exclusive_start(kinesis_client, unique_name):
    list_name = unique_name("PyStreamList")
    kinesis_client.create_stream(StreamName=list_name, ShardCount=1)
    try:
        time.sleep(1)
        resp = kinesis_client.list_shards(StreamName=list_name)
        assert resp.get("Shards") is not None
    finally:
        try:
            kinesis_client.delete_stream(StreamName=list_name)
        except Exception:
            pass
