output "api_service_name" {
  description = "The name of the API ECS service"
  value       = aws_ecs_service.api_service.name
}

output "api_task_definition_arn" {
  description = "The ARN of the API task definition"
  value       = aws_ecs_task_definition.api.arn
}

output "api_service_arn" {
  description = "The ARN of the API service"
  value       = aws_ecs_service.api_service.id
}

output "api_task_definition_family" {
  description = "The ECS Task Definition Family for the API"
  value       = aws_ecs_task_definition.api.family
}