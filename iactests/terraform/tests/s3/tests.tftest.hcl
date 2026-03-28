run "create_basic_bucket" {
  command = apply

  assert {
    condition     = aws_s3_bucket.basic.bucket == "tf-test-basic-bucket"
    error_message = "Bucket name should be tf-test-basic-bucket"
  }

  assert {
    condition     = can(regex("arn:aws:s3:::tf-test-basic-bucket", aws_s3_bucket.basic.arn))
    error_message = "Bucket ARN should contain the bucket name"
  }
}

run "versioned_bucket" {
  command = apply

  assert {
    condition     = length([for c in aws_s3_bucket_versioning.versioned.versioning_configuration : c if c.status == "Enabled"]) > 0
    error_message = "Versioning should be Enabled"
  }
}

run "encrypted_bucket" {
  command = apply

  assert {
    condition     = length(aws_s3_bucket_server_side_encryption_configuration.encrypted.rule) > 0
    error_message = "Encryption configuration should have at least one rule"
  }
}
