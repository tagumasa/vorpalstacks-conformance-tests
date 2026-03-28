run "create_basic_function" {
  command = apply

  assert {
    condition     = aws_lambda_function.basic.function_name == "tf-test-basic-function"
    error_message = "Function name should be tf-test-basic-function"
  }

  assert {
    condition     = aws_lambda_function.basic.runtime == "python3.12"
    error_message = "Runtime should be python3.12"
  }

  assert {
    condition     = aws_lambda_function.basic.handler == "handler.handler"
    error_message = "Handler should be handler.handler"
  }

  assert {
    condition     = can(regex("arn:aws:lambda:", aws_lambda_function.basic.arn))
    error_message = "Function ARN should be valid"
  }
}

run "create_function_with_env" {
  command = apply

  assert {
    condition     = length(aws_lambda_function.with_env.environment[0].variables) == 2
    error_message = "Function should have 2 environment variables"
  }

  assert {
    condition     = aws_lambda_function.with_env.environment[0].variables["FOO"] == "bar"
    error_message = "Environment variable FOO should be bar"
  }

  assert {
    condition     = aws_lambda_function.with_env.environment[0].variables["BAZ"] == "qux"
    error_message = "Environment variable BAZ should be qux"
  }
}
