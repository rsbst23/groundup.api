# General Variables
variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
}

variable "aws_region" {
  description = "AWS region for deployment"
  type        = string
  default     = "us-east-1"
}

# Backend Configuration
variable "terraform_state_bucket" {
  description = "S3 bucket name for Terraform state storage"
  type        = string
}

variable "terraform_state_key" {
  description = "Key (file path) for storing Terraform state"
  type        = string
}

variable "terraform_dynamodb_table" {
  description = "DynamoDB table for Terraform state locking"
  type        = string
}

# Networking
variable "vpc_cidr" {
  description = "CIDR block for the VPC"
  type        = string
}

variable "availability_zones" {
  description = "List of availability zones"
  type        = list(string)
}

# ECS Cluster
variable "ecs_cluster_name" {
  description = "Name of the ECS cluster"
  type        = string
}

# ECS Task Definition
variable "ecs_task_cpu" {
  description = "CPU units for ECS tasks"
  type        = number
  default     = 256
}

variable "ecs_task_memory" {
  description = "Memory for ECS tasks in MB"
  type        = number
  default     = 512
}

# MySQL Configuration
variable "mysql_image" {
  description = "Docker image for MySQL"
  type        = string
  default     = "mysql:8.0"
}

variable "mysql_port" {
  description = "Port for MySQL database"
  type        = number
  default     = 3306
}

variable "mysql_secrets_arn" {
  description = "AWS Secrets Manager ARN storing MySQL credentials"
  type        = string
}

# Application Load Balancer (ALB)
variable "alb_port" {
  description = "Port for ALB to listen on"
  type        = number
  default     = 80
}

variable "target_group_port" {
  description = "Port for Target Group"
  type        = number
  default     = 8080
}

variable "health_check_path" {
  description = "Path for ALB health check"
  type        = string
  default     = "/swagger/index.html"
}

# Logging
variable "log_group_name" {
  description = "CloudWatch log group name"
  type        = string
}

# ECR Repository
variable "ecr_repository_name" {
  description = "Name of the ECR repository"
  type        = string
}

variable "github_runner_ip" {
  description = "Temporary GitHub Actions Runner IP for MySQL Access"
  type        = string
  default     = ""  # Default to empty so Terraform doesn't fail if not provided
}

variable "api_desired_count" {
  description = "The desired number of running API tasks"
  type        = number
  default     = 1
}

variable "mysql_desired_count" {
  description = "The desired number of running MySQL tasks"
  type        = number
  default     = 1
}