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
    timestreamwrite = "http://localhost:8080"
  }
}

resource "aws_timestreamwrite_database" "test" {
  database_name = "test-db"
}

resource "aws_timestreamwrite_table" "test" {
  database_name = aws_timestreamwrite_database.test.database_name
  table_name    = "test-table"

  retention_properties {
    memory_store_retention_period_in_hours = 24
    magnetic_store_retention_period_in_days = 7
  }
}

data "aws_timestreamwrite_database" "test" {
  name = aws_timestreamwrite_database.test.database_name
}

data "aws_timestreamwrite_table" "test" {
  database_name = aws_timestreamwrite_table.test.database_name
  name          = aws_timestreamwrite_table.test.table_name
}
