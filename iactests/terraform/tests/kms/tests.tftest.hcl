run "create_key" {
  command = apply

  assert {
    condition     = aws_kms_key.basic.description == "Terraform test basic key"
    error_message = "Key description should match"
  }

  assert {
    condition     = aws_kms_key.basic.is_enabled == true
    error_message = "Key should be enabled"
  }

  assert {
    condition     = can(regex("arn:aws:kms:", aws_kms_key.basic.arn))
    error_message = "Key ARN should be a valid KMS ARN"
  }
}

run "create_alias" {
  command = apply

  assert {
    condition     = aws_kms_alias.basic_alias.name == "alias/tf-test-basic"
    error_message = "Alias name should be alias/tf-test-basic"
  }

  assert {
    condition     = aws_kms_alias.basic_alias.target_key_id == aws_kms_key.basic.key_id
    error_message = "Alias should target the basic key"
  }
}

run "create_multi_region_key" {
  command = apply

  assert {
    condition     = aws_kms_key.multi_region.multi_region == true
    error_message = "Key should be multi-region"
  }

  assert {
    condition     = aws_kms_key.multi_region.is_enabled == true
    error_message = "Multi-region key should be enabled"
  }
}

run "create_key_with_policy" {
  command = apply

  assert {
    condition     = aws_kms_key.with_policy.policy != ""
    error_message = "Key should have a policy"
  }

  assert {
    condition     = can(jsondecode(aws_kms_key.with_policy.policy))
    error_message = "Policy should be valid JSON"
  }

  assert {
    condition     = can(regex("kms:\\*", aws_kms_key.with_policy.policy))
    error_message = "Policy should allow kms:* action"
  }
}
