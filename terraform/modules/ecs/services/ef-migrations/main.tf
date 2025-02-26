# Define ECS Task Definition for EF Migrations
resource "aws_ecs_task_definition" "ef_migrations" {
  family                   = "${var.project_name}-ef-migrations"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  execution_role_arn       = var.ecs_task_execution_role_arn
  task_role_arn            = var.ecs_task_execution_role_arn
  cpu                      = var.ecs_task_cpu
  memory                   = var.ecs_task_memory

  container_definitions = jsonencode([
    {
      name      = "${var.project_name}-ef-migrations"
      image = "${var.ecr_ef_migrations_repository_url}:latest"
      essential = true

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        },
        {
          name      = "CLOUDWATCH_LOG_GROUP"
          valueFrom = "${var.cloudwatch_logs_name}"
        }
      ]

      secrets = [
        { name = "MYSQL_SERVER", valueFrom = "${var.mysql_secrets_arn}:host::" },
        { name = "MYSQL_PORT", valueFrom = "${var.mysql_secrets_arn}:port::" },
        { name = "MYSQL_USER", valueFrom = "${var.mysql_secrets_arn}:username::" },
        { name = "MYSQL_PASSWORD", valueFrom = "${var.mysql_secrets_arn}:password::" },
        { name = "MYSQL_DATABASE", valueFrom = "${var.mysql_secrets_arn}:dbname::" }
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

# ECS Run Task for EF Migrations
resource "aws_ecs_service" "ef_migrations_service" {
  name            = "ef-migrations-service"
  cluster         = var.ecs_cluster_id
  task_definition = aws_ecs_task_definition.ef_migrations.arn
  desired_count   = 0  # Ensure the service does not keep running indefinitely

  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.subnets
    security_groups  = [var.ecs_sg_id]
    assign_public_ip = true
  }

  enable_execute_command = true
}
