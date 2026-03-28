terraform {
  required_version = ">= 1.6.0"
  required_providers {
    aws = {
      source  = "opentofu/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region                      = "us-east-1"
  access_key                  = "test"
  secret_key                  = "test"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true
  default_tags {
    tags = {
      ManagedBy = "terraform-test"
    }
  }
  endpoints {
    dynamodb        = "http://localhost:8080"
    s3              = "http://localhost:8080"
    sqs             = "http://localhost:8080"
    sns             = "http://localhost:8080"
    lambda          = "http://localhost:8080"
    iam             = "http://localhost:8080"
    sts             = "http://localhost:8080"
    kms             = "http://localhost:8080"
    cloudwatch      = "http://localhost:8080"
    cloudwatchlogs  = "http://localhost:8080"
    events          = "http://localhost:8080"
    stepfunctions   = "http://localhost:8080"
    apigateway      = "http://localhost:8080"
    route53         = "http://localhost:8080"
    athena          = "http://localhost:8080"
    secretsmanager  = "http://localhost:8080"
    ssm             = "http://localhost:8080"
    scheduler       = "http://localhost:8080"
    kinesis         = "http://localhost:8080"
    cognitoidp      = "http://localhost:8080"
    cognitoidentity = "http://localhost:8080"
    acm             = "http://localhost:8080"
    waf             = "http://localhost:8080"
    wafv2           = "http://localhost:8080"
    ses             = "http://localhost:8080"
    cloudtrail      = "http://localhost:8080"
    timestreamwrite = "http://localhost:8080"
    timestreamquery = "http://localhost:8080"
  }
  s3_use_path_style = true
}

resource "aws_kms_key" "basic" {
  description = "Terraform test basic key"
}

resource "aws_kms_alias" "basic_alias" {
  name          = "alias/tf-test-basic"
  target_key_id = aws_kms_key.basic.key_id
}

resource "aws_kms_key" "multi_region" {
  description             = "Terraform test multi-region key"
  multi_region            = true
  enable_key_rotation     = false
  deletion_window_in_days = 7
}

resource "aws_kms_key" "with_policy" {
  description = "Terraform test key with policy"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { AWS = "*" }
        Action    = "kms:*"
        Resource  = "*"
      }
    ]
  })
}

data "aws_kms_key" "basic" {
  key_id = aws_kms_key.basic.key_id
}

data "aws_kms_key" "multi_region" {
  key_id = aws_kms_key.multi_region.key_id
}

data "aws_kms_key" "with_policy" {
  key_id = aws_kms_key.with_policy.key_id
}

data "aws_kms_alias" "basic_alias" {
  name = aws_kms_alias.basic_alias.name
}
