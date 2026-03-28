run "create_workgroup" {
  command = apply

  assert {
    condition     = aws_athena_workgroup.basic.name == "tf-test-workgroup"
    error_message = "Workgroup name should be tf-test-workgroup"
  }

  assert {
    condition     = length([for c in aws_athena_workgroup.basic.configuration : c if c.enforce_workgroup_configuration == true]) > 0
    error_message = "Workgroup should enforce configuration"
  }

  assert {
    condition     = can(regex("arn:aws:athena:", aws_athena_workgroup.basic.arn))
    error_message = "Workgroup ARN should be a valid Athena ARN"
  }
}
