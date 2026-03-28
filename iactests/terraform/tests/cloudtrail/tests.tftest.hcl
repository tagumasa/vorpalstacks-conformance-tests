run "create_cloudtrail" {
  command = apply

  assert {
    condition     = aws_cloudtrail.basic.name == "tf-test-cloudtrail"
    error_message = "Trail name should be tf-test-cloudtrail"
  }

  assert {
    condition     = aws_cloudtrail.basic.s3_bucket_name == "tf-test-cloudtrail-bucket"
    error_message = "Trail should reference the S3 bucket"
  }

  assert {
    condition     = aws_cloudtrail.basic.enable_logging == true
    error_message = "Trail should have logging enabled"
  }

  assert {
    condition     = can(regex("arn:aws:cloudtrail:", aws_cloudtrail.basic.arn))
    error_message = "Trail ARN should be a valid CloudTrail ARN"
  }
}
