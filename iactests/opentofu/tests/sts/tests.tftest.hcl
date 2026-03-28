run "caller_identity" {
  command = apply

  assert {
    condition     = data.aws_caller_identity.current.account_id == "123456789012"
    error_message = "Account ID should be 123456789012"
  }

  assert {
    condition     = data.aws_caller_identity.current.user_id != ""
    error_message = "User ID should not be empty"
  }

  assert {
    condition     = can(regex("^arn:aws:iam::", data.aws_caller_identity.current.arn))
    error_message = "ARN should start with arn:aws:iam::"
  }
}
