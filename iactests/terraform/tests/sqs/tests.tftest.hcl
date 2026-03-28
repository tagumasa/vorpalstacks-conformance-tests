run "create_standard_queue" {
  command = apply

  assert {
    condition     = aws_sqs_queue.standard.name == "tf-test-standard-queue"
    error_message = "Queue name should be tf-test-standard-queue"
  }

  assert {
    condition     = aws_sqs_queue.standard.fifo_queue == false
    error_message = "Standard queue should not be FIFO"
  }
}

run "create_fifo_queue" {
  command = apply

  assert {
    condition     = aws_sqs_queue.fifo.fifo_queue == true
    error_message = "FIFO queue should have fifo_queue = true"
  }

  assert {
    condition     = aws_sqs_queue.fifo.content_based_deduplication == true
    error_message = "FIFO queue should have content_based_deduplication enabled"
  }

  assert {
    condition     = can(regex("\\.fifo$", aws_sqs_queue.fifo.name))
    error_message = "FIFO queue name should end with .fifo"
  }
}

run "create_queue_with_policy" {
  command = apply

  assert {
    condition     = aws_sqs_queue.with_policy.policy != ""
    error_message = "Queue should have a policy"
  }

  assert {
    condition     = can(jsondecode(aws_sqs_queue.with_policy.policy))
    error_message = "Policy should be valid JSON"
  }
}

run "create_queue_with_dlq" {
  command = apply

  assert {
    condition     = aws_sqs_queue.main_with_dlq.redrive_policy != ""
    error_message = "Queue should have a redrive policy"
  }

  assert {
    condition     = can(regex(aws_sqs_queue.dlq.arn, aws_sqs_queue.main_with_dlq.redrive_policy))
    error_message = "Redrive policy should reference the DLQ ARN"
  }
}
