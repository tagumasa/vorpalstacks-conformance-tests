def test_register_stream_consumer(kinesis_stream_setup, kinesis_client, unique_name):
    consumer_name = unique_name("PyConsumer")
    resp = kinesis_client.register_stream_consumer(
        StreamARN=kinesis_stream_setup["stream_arn"], ConsumerName=consumer_name
    )
    assert resp.get("Consumer"), "Consumer is nil"


def test_describe_stream_consumer(kinesis_stream_setup, kinesis_client, unique_name):
    consumer_name = unique_name("PyConsumer")
    kinesis_client.register_stream_consumer(
        StreamARN=kinesis_stream_setup["stream_arn"], ConsumerName=consumer_name
    )
    resp = kinesis_client.describe_stream_consumer(
        StreamARN=kinesis_stream_setup["stream_arn"], ConsumerName=consumer_name
    )
    assert resp.get("ConsumerDescription"), "ConsumerDescription is nil"
    assert resp["ConsumerDescription"]["ConsumerARN"], (
        "ConsumerDescription.ConsumerARN is nil"
    )


def test_list_stream_consumers(kinesis_stream_setup, kinesis_client, unique_name):
    consumer_name = unique_name("PyConsumer")
    kinesis_client.register_stream_consumer(
        StreamARN=kinesis_stream_setup["stream_arn"], ConsumerName=consumer_name
    )
    resp = kinesis_client.list_stream_consumers(
        StreamARN=kinesis_stream_setup["stream_arn"]
    )
    assert resp.get("Consumers") is not None


def test_deregister_stream_consumer(kinesis_stream_setup, kinesis_client, unique_name):
    consumer_name = unique_name("PyConsumer")
    kinesis_client.register_stream_consumer(
        StreamARN=kinesis_stream_setup["stream_arn"], ConsumerName=consumer_name
    )
    kinesis_client.deregister_stream_consumer(
        StreamARN=kinesis_stream_setup["stream_arn"], ConsumerName=consumer_name
    )
