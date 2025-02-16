# Define ECS Task Definition for API
resource "aws_ecs_task_definition" "api" {
  family                   = "${var.project_name}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  execution_role_arn       = var.ecs_task_execution_role_arn
  task_role_arn            = var.ecs_task_execution_role_arn
  cpu                      = var.ecs_task_cpu
  memory                   = var.ecs_task_memory

  container_definitions = jsonencode([
    {
      name      = "${var.project_name}-api"
      image     = "${var.ecr_api_repository_url}:latest"
      cpu       = var.ecs_task_cpu
      memory    = var.ecs_task_memory
      essential = true

      portMappings = [
        {
          containerPort = 8080
          hostPort      = 8080
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        }
      ]

      secrets = [
        {
          name      = "MYSQL_SERVER"
          valueFrom = "${var.mysql_secrets_arn}:host::"
        },
        {
          name      = "MYSQL_PORT"
          valueFrom = "${var.mysql_secrets_arn}:port::"
        },
        {
          name      = "MYSQL_USER"
          valueFrom = "${var.mysql_secrets_arn}:username::"
        },
        {
          name      = "MYSQL_PASSWORD"
          valueFrom = "${var.mysql_secrets_arn}:password::"
        },
        {
          name      = "MYSQL_DATABASE"
          valueFrom = "${var.mysql_secrets_arn}:dbname::"
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.cloudwatch_logs_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "ecs"
        }
      }
    }
  ])
}

# ECS Service for API
resource "aws_ecs_service" "api_service" {
  name            = "api-service"
  cluster         = var.ecs_cluster_id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.subnets
    security_groups  = [var.ecs_sg_id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = var.api_target_group_arn
    container_name   = "${var.project_name}-api"
    container_port   = 8080
  }

  enable_execute_command = true

  service_registries {
    registry_arn = var.api_service_discovery_arn
  }

  lifecycle {
    prevent_destroy = false  # Allows Terraform to delete the service
  }

  depends_on = [var.alb_listener_arn]
}

