resource "aws_cloudwatch_log_group" "ecs_logs" {
  name = var.log_group_name

  retention_in_days = 30  # Keep logs for 30 days

  tags = {
    Name = "${var.project_name}-ecs-logs"
  }
}