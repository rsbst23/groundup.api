# Create a Service Discovery Namespace for ECS services
resource "aws_service_discovery_private_dns_namespace" "main" {
  name = "${var.project_name}-namespace"
  vpc  = var.vpc_id

  tags = {
    Name = "${var.project_name}-namespace"
  }
}

# Service Discovery Service for API
resource "aws_service_discovery_service" "api_service" {
  name = "api-service"
  namespace_id = aws_service_discovery_private_dns_namespace.main.id

  dns_config {
    namespace_id = aws_service_discovery_private_dns_namespace.main.id
    dns_records {
      ttl  = 10
      type = "A"
    }
    routing_policy = "MULTIVALUE"
  }

  health_check_custom_config {
    failure_threshold = 1
  }

  tags = {
    Name = "${var.project_name}-api-service-discovery"
  }
}

# Service Discovery Service for MySQL
resource "aws_service_discovery_service" "mysql_service" {
  name = "mysql-service"
  namespace_id = aws_service_discovery_private_dns_namespace.main.id

  dns_config {
    namespace_id = aws_service_discovery_private_dns_namespace.main.id
    dns_records {
      ttl  = 10
      type = "A"
    }
    routing_policy = "MULTIVALUE"
  }

  health_check_custom_config {
    failure_threshold = 1
  }

  tags = {
    Name = "${var.project_name}-mysql-service-discovery"
  }
}
