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
    tags = { ManagedBy = "terraform-test" }
  }
  endpoints {
    cognitoidentity = "http://localhost:8080"
  }
}

resource "aws_cognito_identity_pool" "test" {
  identity_pool_name = "test-pool"
}

data "aws_cognito_identity_pool" "test" {
  identity_pool_name = aws_cognito_identity_pool.test.identity_pool_name
}
