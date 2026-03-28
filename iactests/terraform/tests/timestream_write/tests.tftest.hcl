run "create_database" {
  command = apply

  assert {
    condition     = aws_timestreamwrite_database.test.database_name == "test-db"
    error_message = "Database name should be test-db"
  }

  assert {
    condition     = can(regex("arn:aws:timestream:", aws_timestreamwrite_database.test.arn))
    error_message = "Database ARN should contain arn:aws:timestream:"
  }

  assert {
    condition     = aws_timestreamwrite_database.test.table_count >= 0
    error_message = "Table count should be >= 0"
  }
}

run "create_table" {
  command = apply

  assert {
    condition     = aws_timestreamwrite_table.test.table_name == "test-table"
    error_message = "Table name should be test-table"
  }

  assert {
    condition     = aws_timestreamwrite_table.test.database_name == "test-db"
    error_message = "Table database_name should be test-db"
  }

  assert {
    condition     = can(regex("arn:aws:timestream:", aws_timestreamwrite_table.test.arn))
    error_message = "Table ARN should contain arn:aws:timestream:"
  }

  assert {
    condition     = aws_timestreamwrite_table.test.retention_properties[0].memory_store_retention_period_in_hours == 24
    error_message = "Memory store retention should be 24 hours"
  }

  assert {
    condition     = aws_timestreamwrite_table.test.retention_properties[0].magnetic_store_retention_period_in_days == 7
    error_message = "Magnetic store retention should be 7 days"
  }
}
