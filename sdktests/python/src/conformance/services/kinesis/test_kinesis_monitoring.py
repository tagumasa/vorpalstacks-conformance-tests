def test_enable_enhanced_monitoring(kinesis_stream_setup, kinesis_client):
    resp = kinesis_client.enable_enhanced_monitoring(
        StreamName=kinesis_stream_setup["stream_name"],
        ShardLevelMetrics=["IncomingBytes", "OutgoingBytes"],
    )
    assert resp.get("CurrentShardLevelMetrics") is not None


def test_disable_enhanced_monitoring(kinesis_stream_setup, kinesis_client):
    kinesis_client.enable_enhanced_monitoring(
        StreamName=kinesis_stream_setup["stream_name"],
        ShardLevelMetrics=["IncomingBytes", "OutgoingBytes"],
    )
    resp = kinesis_client.disable_enhanced_monitoring(
        StreamName=kinesis_stream_setup["stream_name"],
        ShardLevelMetrics=["IncomingBytes", "OutgoingBytes"],
    )
    assert resp.get("CurrentShardLevelMetrics") is not None
