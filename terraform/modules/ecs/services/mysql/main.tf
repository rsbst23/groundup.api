# Define ECS Task Definition for MySQL
resource "aws_ecs_task_definition" "mysql" {
  family                   = "${var.project_name}-mysql"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  execution_role_arn       = var.ecs_task_execution_role_arn
  task_role_arn            = var.ecs_task_execution_role_arn
  cpu                      = var.ecs_task_cpu
  memory                   = var.ecs_task_memory

  container_definitions = jsonencode([
    {
      name      = "${var.project_name}-mysql"
      image     = "mysql:8.0"
      cpu       = var.ecs_task_cpu
      memory    = var.ecs_task_memory
      essential = true

      portMappings = [
        {
          containerPort = 3306
          hostPort      = 3306
        }
      ]

      secrets = [
        {
          name      = "MYSQL_ROOT_PASSWORD"
          valueFrom = "${var.mysql_secrets_arn}:rootpassword::"
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

# ECS Service for MySQL
resource "aws_ecs_service" "mysql_service" {
  name            = "mysql-service"
  cluster         = var.ecs_cluster_id
  task_definition = aws_ecs_task_definition.mysql.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.subnets
    security_groups  = [var.mysql_sg_id]
    assign_public_ip = true
  }

  service_registries {
    registry_arn = var.mysql_service_discovery_arn
  }

  enable_execute_command = true

  lifecycle {
    prevent_destroy = false  # Allows Terraform to delete the service
  }
}