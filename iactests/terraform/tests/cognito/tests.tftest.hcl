run "create_identity_pool" {
  command = apply

  assert {
    condition     = aws_cognito_identity_pool.basic.identity_pool_name == "tf-test-identity-pool"
    error_message = "Identity pool name should be tf-test-identity-pool"
  }

  assert {
    condition     = aws_cognito_identity_pool.basic.allow_unauthenticated_identities == true
    error_message = "Identity pool should allow unauthenticated identities"
  }

  assert {
    condition     = can(regex("arn:aws:cognito-identity:", aws_cognito_identity_pool.basic.arn))
    error_message = "Identity pool ARN should be a valid Cognito Identity ARN"
  }
}

run "create_user_pool" {
  command = apply

  assert {
    condition     = aws_cognito_user_pool.basic.name == "tf-test-user-pool"
    error_message = "User pool name should be tf-test-user-pool"
  }

  assert {
    condition     = length([for p in aws_cognito_user_pool.basic.password_policy : p if p.minimum_length == 8]) > 0
    error_message = "Password minimum length should be 8"
  }

  assert {
    condition     = can(regex("arn:aws:cognito-idp:", aws_cognito_user_pool.basic.arn))
    error_message = "User pool ARN should be a valid Cognito IDP ARN"
  }
}
