run "create_state_machine" {
  command = apply

  assert {
    condition     = aws_sfn_state_machine.basic.name == "tf-test-state-machine"
    error_message = "State machine name should be tf-test-state-machine"
  }

  assert {
    condition     = can(regex("arn:aws:states:", aws_sfn_state_machine.basic.arn))
    error_message = "State machine ARN should be a valid Step Functions ARN"
  }

  assert {
    condition     = can(jsondecode(aws_sfn_state_machine.basic.definition))
    error_message = "State machine definition should be valid JSON"
  }

  assert {
    condition     = jsondecode(aws_sfn_state_machine.basic.definition).StartAt == "HelloWorld"
    error_message = "StartAt should be HelloWorld"
  }
}
