output "cloudwatch_logs_arn" {
  description = "ARN of the CloudWatch Logs group"
  value       = aws_cloudwatch_log_group.ecs_logs.arn
}
