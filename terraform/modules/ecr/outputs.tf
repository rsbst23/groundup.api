output "ecr_api_repository_url" {
  description = "The URL of the ECR repository for the API"
  value       = aws_ecr_repository.api.repository_url
}

output "ecr_ef_migrations_repository_url" {
  description = "The URL of the ECR repository for EF Migrations"
  value       = aws_ecr_repository.ef_migrations.repository_url
}
