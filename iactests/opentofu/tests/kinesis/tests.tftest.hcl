run "create_basic_stream" {
  command = apply

  assert {
    condition     = aws_kinesis_stream.basic.name == "tf-test-basic-stream"
    error_message = "Stream name should be tf-test-basic-stream"
  }

  assert {
    condition     = aws_kinesis_stream.basic.shard_count == 1
    error_message = "Stream should have 1 shard"
  }

  assert {
    condition     = can(regex("arn:aws:kinesis:", aws_kinesis_stream.basic.arn))
    error_message = "Stream ARN should be valid"
  }
}

run "create_enhanced_stream" {
  command = apply

  assert {
    condition     = aws_kinesis_stream.enhanced.name == "tf-test-enhanced-stream"
    error_message = "Stream name should be tf-test-enhanced-stream"
  }

  assert {
    condition     = aws_kinesis_stream.enhanced.shard_count == 2
    error_message = "Stream should have 2 shards"
  }

  assert {
    condition     = length([for d in aws_kinesis_stream.enhanced.stream_mode_details : d if d.stream_mode == "PROVISIONED"]) > 0
    error_message = "Stream mode should be PROVISIONED"
  }
}
