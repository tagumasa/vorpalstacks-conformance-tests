run "create_schedule_group" {
  command = apply

  assert {
    condition     = aws_scheduler_schedule_group.basic.name == "tf-test-schedule-group"
    error_message = "Schedule group name should be tf-test-schedule-group"
  }
}

run "create_schedule" {
  command = apply

  assert {
    condition     = aws_scheduler_schedule.basic.name == "tf-test-schedule"
    error_message = "Schedule name should be tf-test-schedule"
  }

  assert {
    condition     = aws_scheduler_schedule.basic.schedule_expression == "rate(5 minutes)"
    error_message = "Schedule expression should be rate(5 minutes)"
  }

  assert {
    condition     = length([for w in aws_scheduler_schedule.basic.flexible_time_window : w if w.mode == "OFF"]) > 0
    error_message = "Flexible time window mode should be OFF"
  }

  assert {
    condition     = length([for t in aws_scheduler_schedule.basic.target : t if t.arn == aws_sqs_queue.scheduler_target_queue.arn]) > 0
    error_message = "Schedule target should reference the SQS queue"
  }
}
