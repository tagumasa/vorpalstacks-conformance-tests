run "create_string_parameter" {
  command = apply

  assert {
    condition     = aws_ssm_parameter.string_param.name == "tf-test-string-param"
    error_message = "Parameter name should be tf-test-string-param"
  }

  assert {
    condition     = aws_ssm_parameter.string_param.type == "String"
    error_message = "Parameter type should be String"
  }

  assert {
    condition     = aws_ssm_parameter.string_param.value == "test-value"
    error_message = "Parameter value should be test-value"
  }
}

run "create_secure_string_parameter" {
  command = apply

  assert {
    condition     = aws_ssm_parameter.secure_string_param.name == "tf-test-secure-param"
    error_message = "Parameter name should be tf-test-secure-param"
  }

  assert {
    condition     = aws_ssm_parameter.secure_string_param.type == "SecureString"
    error_message = "Parameter type should be SecureString"
  }
}
