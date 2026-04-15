def test_put_record(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.put_record(
        StreamName=kinesis_stream_setup["stream_name"],
        Data=b"test record data",
        PartitionKey="partition-key-1",
    )
    assert resp.get("SequenceNumber"), "SequenceNumber is null"
    assert resp.get("ShardId"), "ShardId is null"


def test_put_records(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.put_records(
        StreamName=kinesis_stream_setup["stream_name"],
        Records=[
            {"Data": b"record 1", "PartitionKey": "key-1"},
            {"Data": b"record 2", "PartitionKey": "key-2"},
            {"Data": b"record 3", "PartitionKey": "key-3"},
        ],
    )
    assert resp.get("FailedRecordCount") is not None
    assert resp.get("Records") is not None
    for i, rec in enumerate(resp["Records"]):
        assert rec.get("SequenceNumber"), f"record[{i}].SequenceNumber is nil"


def test_get_shard_iterator(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.get_shard_iterator(
        StreamName=kinesis_stream_setup["stream_name"],
        ShardId="shardId-000000000000",
        ShardIteratorType="LATEST",
    )
    assert resp.get("ShardIterator"), "ShardIterator is null"


def test_get_records(kinesis_stream_setup, kinesis_client):
    iter_resp = kinesis_client.get_shard_iterator(
        StreamName=kinesis_stream_setup["stream_name"],
        ShardId="shardId-000000000000",
        ShardIteratorType="LATEST",
    )
    shard_iterator = iter_resp["ShardIterator"]
    resp = kinesis_client.get_records(ShardIterator=shard_iterator, Limit=100)
    assert "Records" in resp or "NextShardIterator" in resp
