output "event_bus_name" {
  description = "Name of the EventBridge event bus"
  value       = aws_cloudwatch_event_bus.main.name
}

output "event_bus_arn" {
  description = "ARN of the EventBridge event bus"
  value       = aws_cloudwatch_event_bus.main.arn
}

output "lambda_function_name" {
  description = "Name of the Hero Image Processor Lambda function"
  value       = aws_lambda_function.hero_image_processor.function_name
}

output "lambda_function_arn" {
  description = "ARN of the Hero Image Processor Lambda function"
  value       = aws_lambda_function.hero_image_processor.arn
}

output "lambda_role_arn" {
  description = "ARN of the Lambda execution role"
  value       = aws_iam_role.hero_image_processor_lambda_role.arn
}

output "dlq_queue_url" {
  description = "URL of the dead-letter queue for failed Lambda invocations"
  value       = aws_sqs_queue.hero_image_processor_dlq.url
}

output "dlq_queue_arn" {
  description = "ARN of the dead-letter queue"
  value       = aws_sqs_queue.hero_image_processor_dlq.arn
}
