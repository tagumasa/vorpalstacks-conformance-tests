def test_add_tags_to_stream(kinesis_stream_setup, kinesis_client):
    kinesis_client.add_tags_to_stream(
        StreamName=kinesis_stream_setup["stream_name"],
        Tags={"Environment": "test", "Owner": "test-user"},
    )


def test_list_tags_for_stream(kinesis_stream_setup, kinesis_client):
    kinesis_client.add_tags_to_stream(
        StreamName=kinesis_stream_setup["stream_name"],
        Tags={"Environment": "test", "Owner": "test-user"},
    )
    resp = kinesis_client.list_tags_for_stream(
        StreamName=kinesis_stream_setup["stream_name"]
    )
    assert resp.get("Tags") is not None


def test_remove_tags_from_stream(kinesis_stream_setup, kinesis_client):
    kinesis_client.add_tags_to_stream(
        StreamName=kinesis_stream_setup["stream_name"],
        Tags={"Environment": "test"},
    )
    kinesis_client.remove_tags_from_stream(
        StreamName=kinesis_stream_setup["stream_name"], TagKeys=["Environment"]
    )


def test_create_stream_with_tags(kinesis_client, unique_name):
    tags_name = unique_name("PyStreamTags")
    kinesis_client.create_stream(
        StreamName=tags_name,
        ShardCount=1,
        Tags={"Environment": "test", "Owner": "test-user"},
    )
    try:
        pass
    finally:
        try:
            kinesis_client.delete_stream(StreamName=tags_name)
        except Exception:
            pass
