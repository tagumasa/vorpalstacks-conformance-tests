run "create_basic_log_group" {
  command = apply

  assert {
    condition     = aws_cloudwatch_log_group.basic.name == "/terraform/test/basic"
    error_message = "Log group name should be /terraform/test/basic"
  }

  assert {
    condition     = aws_cloudwatch_log_group.basic.retention_in_days == 30
    error_message = "Retention should be 30 days"
  }
}

run "create_custom_retention_log_group" {
  command = apply

  assert {
    condition     = aws_cloudwatch_log_group.custom_retention.name == "/terraform/test/custom-retention"
    error_message = "Log group name should be /terraform/test/custom-retention"
  }

  assert {
    condition     = aws_cloudwatch_log_group.custom_retention.retention_in_days == 14
    error_message = "Retention should be 14 days"
  }
}

run "create_log_stream" {
  command = apply

  assert {
    condition     = aws_cloudwatch_log_stream.test_stream.name == "tf-test-stream"
    error_message = "Log stream name should be tf-test-stream"
  }

  assert {
    condition     = aws_cloudwatch_log_stream.test_stream.log_group_name == "/terraform/test/basic"
    error_message = "Log stream should belong to /terraform/test/basic"
  }
}
