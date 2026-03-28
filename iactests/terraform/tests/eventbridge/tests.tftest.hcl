run "create_pattern_rule" {
  command = apply

  assert {
    condition     = aws_cloudwatch_event_rule.pattern_rule.name == "tf-test-pattern-rule"
    error_message = "Rule name should be tf-test-pattern-rule"
  }

  assert {
    condition     = aws_cloudwatch_event_rule.pattern_rule.event_pattern != ""
    error_message = "Rule should have an event pattern"
  }

  assert {
    condition     = can(jsondecode(aws_cloudwatch_event_rule.pattern_rule.event_pattern))
    error_message = "Event pattern should be valid JSON"
  }
}

run "create_schedule_rule_with_target" {
  command = apply

  assert {
    condition     = aws_cloudwatch_event_rule.schedule_rule.schedule_expression == "rate(5 minutes)"
    error_message = "Schedule expression should be rate(5 minutes)"
  }

  assert {
    condition     = aws_cloudwatch_event_target.sqs_target.arn == aws_sqs_queue.target_queue.arn
    error_message = "Event target should reference the SQS queue ARN"
  }

  assert {
    condition     = aws_cloudwatch_event_target.sqs_target.target_id == "sqs-target"
    error_message = "Event target ID should be sqs-target"
  }
}
