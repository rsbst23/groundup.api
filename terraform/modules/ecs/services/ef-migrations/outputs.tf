output "ef_migrations_service_name" {
  description = "The name of the EF Migrations ECS service"
  value       = aws_ecs_service.ef_migrations_service.name
}

output "ef_migrations_task_definition_arn" {
  description = "The ARN of the EF Migrations task definition"
  value       = aws_ecs_task_definition.ef_migrations.arn
}

output "ef_migrations_service_arn" {
  description = "The ARN of the EF Migrations service"
  value       = aws_ecs_service.ef_migrations_service.id
}