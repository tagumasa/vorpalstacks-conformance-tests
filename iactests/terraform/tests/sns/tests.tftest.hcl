run "create_basic_topic" {
  command = apply

  assert {
    condition     = aws_sns_topic.basic.name == "tf-test-basic-topic"
    error_message = "Topic name should be tf-test-basic-topic"
  }

  assert {
    condition     = can(regex("arn:aws:sns:", aws_sns_topic.basic.arn))
    error_message = "Topic ARN should be a valid SNS ARN"
  }
}

run "create_topic_with_policy" {
  command = apply

  assert {
    condition     = aws_sns_topic.with_policy.policy != ""
    error_message = "Topic should have a policy"
  }

  assert {
    condition     = can(jsondecode(aws_sns_topic.with_policy.policy))
    error_message = "Policy should be valid JSON"
  }
}

run "create_topic_with_tags" {
  command = apply

  assert {
    condition     = aws_sns_topic.with_tags.tags["Environment"] == "test"
    error_message = "Topic should have Environment tag set to test"
  }

  assert {
    condition     = aws_sns_topic.with_tags.tags["Service"] == "terraform"
    error_message = "Topic should have Service tag set to terraform"
  }
}
