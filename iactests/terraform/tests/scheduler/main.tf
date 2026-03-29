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

resource "aws_scheduler_schedule_group" "basic" {
  name = "tf-test-schedule-group"
}

resource "aws_sqs_queue" "scheduler_target_queue" {
  name = "tf-test-scheduler-target-queue"
}

data "aws_iam_policy_document" "scheduler_assume" {
  statement {
    effect = "Allow"
    principals {
      type        = "Service"
      identifiers = ["scheduler.amazonaws.com"]
    }
    actions = ["sts:AssumeRole"]
  }
}

resource "aws_iam_role" "scheduler_role" {
  name               = "tf-test-scheduler-role"
  assume_role_policy = data.aws_iam_policy_document.scheduler_assume.json
}

resource "aws_scheduler_schedule" "basic" {
  name       = "tf-test-schedule"
  group_name = aws_scheduler_schedule_group.basic.name

  flexible_time_window {
    mode = "OFF"
  }

  schedule_expression = "rate(5 minutes)"
  target {
    arn     = aws_sqs_queue.scheduler_target_queue.arn
    role_arn = aws_iam_role.scheduler_role.arn
  }
}


