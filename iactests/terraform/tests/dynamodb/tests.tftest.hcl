run "create_basic_table" {
  command = apply

  assert {
    condition     = aws_dynamodb_table.basic.name == "tf-test-basic"
    error_message = "Table name should be tf-test-basic"
  }

  assert {
    condition     = aws_dynamodb_table.basic.billing_mode == "PAY_PER_REQUEST"
    error_message = "Billing mode should be PAY_PER_REQUEST"
  }

  assert {
    condition     = aws_dynamodb_table.basic.hash_key == "id"
    error_message = "Hash key should be id"
  }
}

run "create_table_with_gsi" {
  command = apply

  assert {
    condition     = length(aws_dynamodb_table.with_gsi.global_secondary_index) == 1
    error_message = "Table should have 1 global secondary index"
  }

  assert {
    condition     = length([for i in aws_dynamodb_table.with_gsi.global_secondary_index : i.name]) > 0
    error_message = "GSI should exist"
  }

  assert {
    condition     = length([for i in aws_dynamodb_table.with_gsi.global_secondary_index : i if i.projection_type == "ALL"]) > 0
    error_message = "GSI projection type should be ALL"
  }
}

run "create_table_with_stream" {
  command = apply

  assert {
    condition     = aws_dynamodb_table.with_stream.stream_enabled == true
    error_message = "Stream should be enabled"
  }

  assert {
    condition     = aws_dynamodb_table.with_stream.stream_view_type == "NEW_AND_OLD_IMAGES"
    error_message = "Stream view type should be NEW_AND_OLD_IMAGES"
  }

  assert {
    condition     = can(regex("arn:aws:dynamodb:", aws_dynamodb_table.with_stream.stream_arn))
    error_message = "Stream ARN should be a valid DynamoDB ARN"
  }
}
