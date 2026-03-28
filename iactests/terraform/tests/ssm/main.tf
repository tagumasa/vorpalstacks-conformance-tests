terraform {
  required_version = ">= 1.6.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
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

resource "aws_ssm_parameter" "string_param" {
  name  = "tf-test-string-param"
  type  = "String"
  value = "test-value"
}

resource "aws_ssm_parameter" "secure_string_param" {
  name  = "tf-test-secure-param"
  type  = "SecureString"
  value = "secret-value"
}

data "aws_ssm_parameter" "string_param" {
  name = aws_ssm_parameter.string_param.name
}

data "aws_ssm_parameter" "secure_string_param" {
  name = aws_ssm_parameter.secure_string_param.name
}
