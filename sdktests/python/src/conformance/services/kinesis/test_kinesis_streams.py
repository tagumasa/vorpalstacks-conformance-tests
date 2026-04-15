import time

import pytest


def test_create_stream(kinesis_client, unique_name, region):
    stream_name = unique_name("PyStream")
    kinesis_client.create_stream(StreamName=stream_name, ShardCount=1)
    try:
        for _ in range(30):
            resp = kinesis_client.describe_stream(StreamName=stream_name)
            if resp["StreamDescription"]["StreamStatus"] == "ACTIVE":
                break
            time.sleep(1)
    finally:
        try:
            kinesis_client.delete_stream(StreamName=stream_name)
        except Exception:
            pass


def test_describe_stream(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.describe_stream(
        StreamName=kinesis_stream_setup["stream_name"]
    )
    assert resp["StreamDescription"]["StreamStatus"] == "ACTIVE"


def test_describe_stream_summary(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.describe_stream_summary(
        StreamName=kinesis_stream_setup["stream_name"]
    )
    assert resp.get("StreamDescriptionSummary"), "StreamDescriptionSummary is null"


def test_list_streams(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.list_streams()
    assert resp.get("StreamNames") is not None
    assert kinesis_stream_setup["stream_name"] in resp["StreamNames"]


def test_delete_stream(kinesis_client, unique_name):
    stream_name = unique_name("PyStream")
    kinesis_client.create_stream(StreamName=stream_name, ShardCount=1)
    for _ in range(30):
        resp = kinesis_client.describe_stream(StreamName=stream_name)
        if resp["StreamDescription"]["StreamStatus"] == "ACTIVE":
            break
        time.sleep(1)
    kinesis_client.delete_stream(StreamName=stream_name)


def test_update_shard_count(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.update_shard_count(
        StreamName=kinesis_stream_setup["stream_name"],
        TargetShardCount=2,
        ScalingType="UNIFORM_SCALING",
    )
    assert resp.get("CurrentShardCount") is not None


def test_describe_limits(kinesis_client):
    resp = kinesis_client.describe_limits()
    assert resp.get("ShardLimit") is not None


def test_update_stream_mode(kinesis_client, unique_name):
    stream_name = unique_name("PyStreamMode")
    kinesis_client.create_stream(StreamName=stream_name, ShardCount=1)
    try:
        time.sleep(1)
        desc_resp = kinesis_client.describe_stream(StreamName=stream_name)
        mode_arn = desc_resp["StreamDescription"]["StreamARN"]
        kinesis_client.update_stream_mode(
            StreamARN=mode_arn,
            StreamModeDetails={"StreamMode": "ON_DEMAND"},
        )
    finally:
        try:
            kinesis_client.delete_stream(StreamName=stream_name)
        except Exception:
            pass


def test_increase_stream_retention_period(kinesis_stream_setup, kinesis_client):
    kinesis_client.increase_stream_retention_period(
        StreamName=kinesis_stream_setup["stream_name"], RetentionPeriodHours=48
    )


def test_decrease_stream_retention_period(kinesis_stream_setup, kinesis_client):
    kinesis_client.increase_stream_retention_period(
        StreamName=kinesis_stream_setup["stream_name"], RetentionPeriodHours=48
    )
    kinesis_client.decrease_stream_retention_period(
        StreamName=kinesis_stream_setup["stream_name"], RetentionPeriodHours=24
    )
