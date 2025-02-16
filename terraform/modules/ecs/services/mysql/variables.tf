variable "project_name" {
  description = "The name of the project"
  type        = string
}

variable "ecs_cluster_id" {
  description = "ID of the ECS cluster"
  type        = string
}

variable "ecs_task_execution_role_arn" {
  description = "ARN of the IAM role for ECS task execution"
  type        = string
}

variable "mysql_sg_id" {
  description = "Security group ID for the ECS MySQL service"
  type        = string
}

variable "subnets" {
  description = "List of subnet IDs for ECS service deployment"
  type        = list(string)
}

variable "mysql_service_discovery_arn" {
  description = "Service Discovery ARN for the MySQL service"
  type        = string
}

variable "ecs_task_cpu" {
  description = "CPU allocation for ECS task"
  type        = number
}

variable "ecs_task_memory" {
  description = "Memory allocation for ECS task"
  type        = number
}

variable "cloudwatch_logs_name" {
  description = "CloudWatch Logs group name for MySQL logs"
  type        = string
}

variable "aws_region" {
  description = "AWS region"
  type        = string
}

variable "mysql_secrets_arn" {
  description = "Secrets Manager ARN for MySQL credentials"
  type        = string
}

variable "desired_count" {
  description = "The desired number of running API tasks"
  type        = number
  default     = 1
}