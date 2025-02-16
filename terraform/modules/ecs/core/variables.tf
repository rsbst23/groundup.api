variable "project_name" {
  description = "The name of the project"
  type        = string
}

variable "ecs_cluster_name" {
  description = "The name of the ECS cluster"
  type        = string
}

variable "secrets_manager_arn" {
  description = "ARN of the AWS Secrets Manager storing credentials"
  type        = string
}

variable "cloudwatch_logs_arn" {
  description = "ARN of the CloudWatch Logs group"
  type        = string
}