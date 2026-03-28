terraform {
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

  endpoints {
    wafv2 = "http://localhost:8080"
  }

  default_tags {
    tags = {
      Environment = "test"
      ManagedBy   = "terraform"
    }
  }
}

resource "aws_wafv2_ip_set" "test" {
  name               = "test-ipset"
  scope              = "REGIONAL"
  ip_address_version = "IPV4"
  addresses          = ["192.0.2.0/24", "203.0.113.0/24"]
  description        = "Test IP set for WAF v2"
}

resource "aws_wafv2_regex_pattern_set" "test" {
  name  = "test-regex-set"
  scope = "REGIONAL"

  regular_expression {
    regex_string = "[a-z]+@[a-z]+\\.[a-z]+"
  }

  description = "Test regex pattern set for WAF v2"
}

resource "aws_wafv2_rule_group" "test" {
  name        = "test-rule-group"
  scope       = "REGIONAL"
  capacity    = 100
  description = "Test rule group for WAF v2"

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "test-rule-group"
    sampled_requests_enabled   = true
  }

  rule {
    name     = "block-bad-ips"
    priority = 1
    action {
      block {}
    }
    statement {
      ip_set_reference_statement {
        arn = aws_wafv2_ip_set.test.arn
      }
    }
    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "block-bad-ips"
      sampled_requests_enabled   = true
    }
  }
}

resource "aws_wafv2_web_acl" "test" {
  name        = "test-webacl"
  scope       = "REGIONAL"
  description = "Test web ACL for WAF v2"

  default_action {
    allow {}
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "test-webacl"
    sampled_requests_enabled   = true
  }

  rule {
    name     = "block-bad-ips"
    priority = 1
    action {
      block {}
    }
    statement {
      ip_set_reference_statement {
        arn = aws_wafv2_ip_set.test.arn
      }
    }
    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "block-bad-ips"
      sampled_requests_enabled   = true
    }
  }
}

output "ip_set_arn" {
  value = aws_wafv2_ip_set.test.arn
}

output "ip_set_id" {
  value = aws_wafv2_ip_set.test.id
}

output "regex_pattern_set_arn" {
  value = aws_wafv2_regex_pattern_set.test.arn
}

output "rule_group_arn" {
  value = aws_wafv2_rule_group.test.arn
}

output "web_acl_arn" {
  value = aws_wafv2_web_acl.test.arn
}

data "aws_wafv2_ip_set" "test" {
  name  = aws_wafv2_ip_set.test.name
  scope = "REGIONAL"
}

data "aws_wafv2_regex_pattern_set" "test" {
  name  = aws_wafv2_regex_pattern_set.test.name
  scope = "REGIONAL"
}

data "aws_wafv2_rule_group" "test" {
  name  = aws_wafv2_rule_group.test.name
  scope = "REGIONAL"
}

data "aws_wafv2_web_acl" "test" {
  name  = aws_wafv2_web_acl.test.name
  scope = "REGIONAL"
}
