variable "aws_region" {
  description = "AWS region for resources"
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "cloudflare_r2_account_id" {
  description = "Cloudflare R2 account ID"
  type        = string
  sensitive   = true
}

variable "cloudflare_r2_access_key_id" {
  description = "Cloudflare R2 access key ID"
  type        = string
  sensitive   = true
}

variable "cloudflare_r2_secret_access_key" {
  description = "Cloudflare R2 secret access key"
  type        = string
  sensitive   = true
}

variable "cloudflare_r2_bucket_name" {
  description = "Cloudflare R2 bucket name"
  type        = string
}

variable "lambda_source_code_hash" {
  description = "Base64 encoded SHA256 hash of the Lambda function code"
  type        = string
  default     = "placeholder"
}
