output "mysql_service_name" {
  description = "The name of the MySQL ECS service"
  value       = aws_ecs_service.mysql_service.name
}

output "mysql_task_definition_arn" {
  description = "The ARN of the MySQL task definition"
  value       = aws_ecs_task_definition.mysql.arn
}

output "mysql_service_arn" {
  description = "The ARN of the MySQL service"
  value       = aws_ecs_service.mysql_service.id
}