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
    tags = { ManagedBy = "terraform-test" }
  }
  endpoints {
    cloudwatch = "http://localhost:8080"
  }
}

resource "aws_cloudwatch_metric_alarm" "test" {
  alarm_name          = "test-alarm"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  period              = 60
  statistic           = "Average"
  threshold           = 80

}

data "aws_cloudwatch_metric_alarm" "test" {
  alarm_name = aws_cloudwatch_metric_alarm.test.alarm_name
}
