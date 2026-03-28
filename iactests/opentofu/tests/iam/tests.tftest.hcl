run "create_role" {
  command = apply

  assert {
    condition     = aws_iam_role.lambda_role.name == "tf-test-lambda-role"
    error_message = "Role name should be tf-test-lambda-role"
  }

  assert {
    condition     = can(regex("lambda.amazonaws.com", aws_iam_role.lambda_role.assume_role_policy))
    error_message = "Assume role policy should allow lambda.amazonaws.com"
  }

  assert {
    condition     = can(regex("sts:AssumeRole", aws_iam_role.lambda_role.assume_role_policy))
    error_message = "Assume role policy should allow sts:AssumeRole"
  }
}

run "create_user" {
  command = apply

  assert {
    condition     = aws_iam_user.test_user.name == "tf-test-user"
    error_message = "User name should be tf-test-user"
  }

  assert {
    condition     = can(regex("arn:aws:iam::", aws_iam_user.test_user.arn))
    error_message = "User should have a valid IAM ARN"
  }
}

run "create_group" {
  command = apply

  assert {
    condition     = aws_iam_group.test_group.name == "tf-test-group"
    error_message = "Group name should be tf-test-group"
  }

  assert {
    condition     = contains(aws_iam_group_membership.test.users, "tf-test-user")
    error_message = "Group should contain tf-test-user"
  }
}

run "attach_policy_to_role" {
  command = apply

  assert {
    condition     = aws_iam_role_policy_attachment.lambda_basic.role == "tf-test-lambda-role"
    error_message = "Policy attachment should be on tf-test-lambda-role"
  }

  assert {
    condition     = can(regex("AWSLambdaBasicExecutionRole", aws_iam_role_policy_attachment.lambda_basic.policy_arn))
    error_message = "Policy ARN should reference AWSLambdaBasicExecutionRole"
  }
}
