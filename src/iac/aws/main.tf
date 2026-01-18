#
# Hubletix Module, for all things hubletix.com.
#

terraform {
  # Reusable modules, like app modules, should contrain only the min allowed version.
  required_version = ">= 1.14.0"

  required_providers {
    # NOTE: Changing these values means you need to change values in test-main.tf as well.
    aws = {
      source  = "hashicorp/aws"
      # 6.28 - Latest at time of creation
      version = ">= 6.28"
    }
  }
}

locals {
    app_name = "hubletix"
}

# Will be using a single S3 bucket to store all lambda functions.
# https://registry.terraform.io/providers/hashicorp/aws/latest/docs/resources/s3_bucket
resource "aws_s3_bucket" "lambda_bucket" {
  bucket = "${local.app_name}-lambdas-${var.environment}"
}

# https://registry.terraform.io/providers/hashicorp/aws/latest/docs/resources/s3_bucket_ownership_controls
resource "aws_s3_bucket_ownership_controls" "lambda_bucket" {
  bucket = aws_s3_bucket.lambda_bucket.id

  rule {
    # All objects in the bucket are immediately owned by the bucket owner,
    # regardless of who uploaded them.
    object_ownership = "BucketOwnerEnforced"
  }
}

# https://registry.terraform.io/providers/hashicorp/aws/latest/docs/resources/s3_bucket_acl
resource "aws_s3_bucket_acl" "lambda_bucket" {
  depends_on = [aws_s3_bucket_ownership_controls.lambda_bucket]

  bucket = aws_s3_bucket.lambda_bucket.id
  acl    = "private"
}
