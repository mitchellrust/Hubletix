terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

# ============================================================================
# EventBridge Configuration
# ============================================================================

resource "aws_cloudwatch_event_bus" "main" {
  name = "hubletix-events-${var.environment}"

  tags = {
    Name        = "hubletix-events-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_cloudwatch_event_rule" "hero_image_updated" {
  name           = "hubletix-hero-image-updated-${var.environment}"
  event_bus_name = aws_cloudwatch_event_bus.main.name
  description    = "Route HeroImageUpdated events to Lambda for processing"

  event_pattern = jsonencode({
    source      = ["hubletix.api"]
    detail-type = ["HeroImageUpdated"]
  })

  tags = {
    Name        = "hubletix-hero-image-updated-${var.environment}"
    Environment = var.environment
  }
}

# ============================================================================
# SQS Dead Letter Queue for Lambda failures
# ============================================================================

resource "aws_sqs_queue" "hero_image_processor_dlq" {
  name                      = "hubletix-hero-image-processor-dlq-${var.environment}.fifo"
  fifo_queue                = true
  content_based_deduplication = true
  message_retention_seconds = 1209600  # 14 days
  
  tags = {
    Name        = "hubletix-hero-image-processor-dlq-${var.environment}"
    Environment = var.environment
  }
}

# ============================================================================
# Lambda Execution IAM Role
# ============================================================================

resource "aws_iam_role" "hero_image_processor_lambda_role" {
  name               = "hubletix-hero-image-processor-lambda-role-${var.environment}"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume_role_policy.json

  tags = {
    Name        = "hubletix-hero-image-processor-lambda-role-${var.environment}"
    Environment = var.environment
  }
}

data "aws_iam_policy_document" "lambda_assume_role_policy" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

# CloudWatch Logs policy
resource "aws_iam_role_policy" "lambda_cloudwatch_logs" {
  name   = "hubletix-hero-image-processor-cloudwatch-logs-${var.environment}"
  role   = aws_iam_role.hero_image_processor_lambda_role.id
  policy = data.aws_iam_policy_document.lambda_cloudwatch_logs.json
}

data "aws_iam_policy_document" "lambda_cloudwatch_logs" {
  statement {
    actions = [
      "logs:CreateLogGroup",
      "logs:CreateLogStream",
      "logs:PutLogEvents"
    ]

    resources = ["arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/lambda/*"]
  }
}

# SQS DLQ policy for Lambda retries
resource "aws_iam_role_policy" "lambda_sqs_dlq" {
  name   = "hubletix-hero-image-processor-sqs-dlq-${var.environment}"
  role   = aws_iam_role.hero_image_processor_lambda_role.id
  policy = data.aws_iam_policy_document.lambda_sqs_dlq.json
}

data "aws_iam_policy_document" "lambda_sqs_dlq" {
  statement {
    actions = [
      "sqs:SendMessage"
    ]

    resources = [aws_sqs_queue.hero_image_processor_dlq.arn]
  }
}

# ============================================================================
# Lambda Function
# ============================================================================

resource "aws_lambda_function" "hero_image_processor" {
  # This will be populated by deployment script
  # For now, using a placeholder that must be updated during deployment
  filename = "placeholder.zip"

  function_name = "hubletix-hero-image-processor-${var.environment}"
  role          = aws_iam_role.hero_image_processor_lambda_role.arn
  handler       = "Hubletix.ImageProcessor::Hubletix.ImageProcessor.Handlers.HeroImageEventHandler::HandleAsync"
  runtime       = "dotnet10"
  timeout       = 300  # 5 minutes for image processing
  memory_size   = 3008 # Max allowed for best performance

  environment {
    variables = {
      R2_ACCOUNT_ID         = var.cloudflare_r2_account_id
      R2_ACCESS_KEY_ID      = var.cloudflare_r2_access_key_id
      R2_SECRET_ACCESS_KEY  = var.cloudflare_r2_secret_access_key
      R2_BUCKET_NAME        = var.cloudflare_r2_bucket_name
    }
  }

  dead_letter_config {
    target_arn = aws_sqs_queue.hero_image_processor_dlq.arn
  }

  # Skip creating a default function code - will be updated by CI/CD
  source_code_hash = var.lambda_source_code_hash

  tags = {
    Name        = "hubletix-hero-image-processor-${var.environment}"
    Environment = var.environment
  }
}

# Allow EventBridge to invoke Lambda
resource "aws_lambda_permission" "allow_eventbridge" {
  statement_id  = "AllowExecutionFromEventBridge"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.hero_image_processor.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.hero_image_updated.arn
}

# EventBridge target for Lambda
resource "aws_cloudwatch_event_target" "lambda_target" {
  rule      = aws_cloudwatch_event_rule.hero_image_updated.name
  target_id = "HeroImageProcessorLambda"
  arn       = aws_lambda_function.hero_image_processor.arn
  role_arn  = aws_iam_role.eventbridge_invoke_lambda_role.arn

  event_bus_name = aws_cloudwatch_event_bus.main.name

  # Retry policy: 1 retry with 60 second delay
  retry_policy {
    maximum_event_age       = 3600
    maximum_retry_attempts  = 1
  }

  # DLQ for failed events
  dead_letter_config {
    arn = aws_sqs_queue.hero_image_processor_dlq.arn
  }
}

# ============================================================================
# EventBridge Invoke Lambda IAM Role
# ============================================================================

resource "aws_iam_role" "eventbridge_invoke_lambda_role" {
  name               = "hubletix-eventbridge-invoke-lambda-role-${var.environment}"
  assume_role_policy = data.aws_iam_policy_document.eventbridge_assume_role_policy.json

  tags = {
    Name        = "hubletix-eventbridge-invoke-lambda-role-${var.environment}"
    Environment = var.environment
  }
}

data "aws_iam_policy_document" "eventbridge_assume_role_policy" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["events.amazonaws.com"]
    }
  }
}

# Allow EventBridge to invoke Lambda and send to DLQ
resource "aws_iam_role_policy" "eventbridge_lambda_invoke" {
  name   = "hubletix-eventbridge-lambda-invoke-${var.environment}"
  role   = aws_iam_role.eventbridge_invoke_lambda_role.id
  policy = data.aws_iam_policy_document.eventbridge_lambda_invoke.json
}

data "aws_iam_policy_document" "eventbridge_lambda_invoke" {
  statement {
    actions = [
      "lambda:InvokeFunction"
    ]

    resources = [aws_lambda_function.hero_image_processor.arn]
  }

  statement {
    actions = [
      "sqs:SendMessage"
    ]

    resources = [aws_sqs_queue.hero_image_processor_dlq.arn]
  }
}

# ============================================================================
# Data Sources
# ============================================================================

data "aws_caller_identity" "current" {}
