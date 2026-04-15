kms_key = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"


def test_start_stream_encryption(kinesis_stream_setup, kinesis_client):
    kinesis_client.start_stream_encryption(
        StreamName=kinesis_stream_setup["stream_name"],
        EncryptionType="KMS",
        KeyId=kms_key,
    )


def test_stop_stream_encryption(kinesis_stream_setup, kinesis_client):
    kinesis_client.start_stream_encryption(
        StreamName=kinesis_stream_setup["stream_name"],
        EncryptionType="KMS",
        KeyId=kms_key,
    )
    kinesis_client.stop_stream_encryption(
        StreamName=kinesis_stream_setup["stream_name"],
        EncryptionType="KMS",
        KeyId=kms_key,
    )
