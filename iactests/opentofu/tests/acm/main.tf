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
    acm = "http://localhost:8080"
  }
}

resource "tls_private_key" "test" {
  algorithm = "RSA"
}

resource "tls_self_signed_cert" "test" {
  private_key_pem       = tls_private_key.test.private_key_pem
  subject {
    common_name  = "example.com"
    organization = "Test Org"
  }
  validity_period_hours = 1
  allowed_uses = [
    "key_encipherment",
    "digital_signature",
    "server_auth",
  ]
}

resource "aws_acm_certificate" "test" {
  certificate_body = tls_self_signed_cert.test.cert_pem
  private_key      = tls_private_key.test.private_key_pem

}

data "aws_acm_certificate" "test" {
  domain   = "example.com"
  statuses = ["ISSUED"]
}
