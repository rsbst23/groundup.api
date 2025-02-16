output "service_discovery_namespace_id" {
  description = "ID of the service discovery namespace"
  value       = aws_service_discovery_private_dns_namespace.main.id
}

output "api_service_discovery_arn" {
  description = "The ARN of the service discovery service for the API"
  value       = aws_service_discovery_service.api_service.arn  # Fixes incorrect reference
}

output "mysql_service_discovery_arn" {
  description = "The ARN of the service discovery service for MySQL"
  value       = aws_service_discovery_service.mysql_service.arn  # Fixes incorrect reference
}