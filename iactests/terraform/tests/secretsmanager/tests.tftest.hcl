run "create_basic_secret" {
  command = apply

  assert {
    condition     = aws_secretsmanager_secret.basic.name == "tf-test-basic-secret"
    error_message = "Secret name should be tf-test-basic-secret"
  }

  assert {
    condition     = can(regex("arn:aws:secretsmanager:", aws_secretsmanager_secret.basic.arn))
    error_message = "Secret ARN should be a valid Secrets Manager ARN"
  }
}

run "create_secret_with_version" {
  command = apply

  assert {
    condition     = aws_secretsmanager_secret.with_value.name == "tf-test-secret-with-value"
    error_message = "Secret name should be tf-test-secret-with-value"
  }

  assert {
    condition     = aws_secretsmanager_secret_version.with_value.secret_string != ""
    error_message = "Secret version should have a secret_string value"
  }

  assert {
    condition     = can(jsondecode(aws_secretsmanager_secret_version.with_value.secret_string))
    error_message = "Secret string should be valid JSON"
  }
}
