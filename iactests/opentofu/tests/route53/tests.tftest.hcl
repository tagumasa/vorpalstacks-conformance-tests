run "create_hosted_zone" {
  command = apply

  assert {
    condition     = aws_route53_zone.basic.name == "tf-test.example.com"
    error_message = "Zone name should be tf-test.example.com"
  }

  assert {
    condition     = can(regex("arn:aws:route53::", aws_route53_zone.basic.arn))
    error_message = "Zone ARN should be a valid Route53 ARN"
  }
}

run "create_record" {
  command = apply

  assert {
    condition     = aws_route53_record.basic.name == "www.tf-test.example.com"
    error_message = "Record name should be www.tf-test.example.com"
  }

  assert {
    condition     = aws_route53_record.basic.type == "A"
    error_message = "Record type should be A"
  }

  assert {
    condition     = aws_route53_record.basic.ttl == 300
    error_message = "Record TTL should be 300"
  }

  assert {
    condition     = contains(aws_route53_record.basic.records, "10.0.0.1")
    error_message = "Record should point to 10.0.0.1"
  }
}
