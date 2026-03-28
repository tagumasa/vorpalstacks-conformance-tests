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
    waf = "http://localhost:8080"
  }
}

resource "aws_waf_ipset" "test" {
  name = "test-ipset"

  ip_set_descriptors {
    type  = "IPV4"
    value = "192.0.2.0/24"
  }

  ip_set_descriptors {
    type  = "IPV4"
    value = "203.0.113.0/24"
  }
}

resource "aws_waf_regex_pattern_set" "test" {
  name                  = "test-regex-set"
  regex_pattern_strings = ["[a-z]+@[a-z]+\\.[a-z]+"]
}

resource "aws_waf_rule" "test" {
  name        = "test-rule"
  metric_name = "testrule"

  predicates {
    data_id = aws_waf_ipset.test.id
    negated = false
    type    = "IPMatch"
  }
}

resource "aws_waf_web_acl" "test" {
  name        = "test-webacl"
  metric_name = "testwebacl"

  default_action {
    type = "ALLOW"
  }

  rules {
    action {
      type = "BLOCK"
    }

    priority = 1
    rule_id  = aws_waf_rule.test.id
    type     = "REGULAR"
  }
}

data "aws_waf_ipset" "test" {
  name = aws_waf_ipset.test.name
}

data "aws_waf_regex_pattern_set" "test" {
  name = aws_waf_regex_pattern_set.test.name
}

data "aws_waf_rule" "test" {
  name = aws_waf_rule.test.name
}

data "aws_waf_web_acl" "test" {
  name = aws_waf_web_acl.test.name
}
