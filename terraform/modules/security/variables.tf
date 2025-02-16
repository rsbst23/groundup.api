variable "project_name" {
  description = "The name of the project"
  type        = string
}

variable "vpc_id" {
  description = "VPC ID where the security groups will be created"
  type        = string
}

variable "github_runner_ip" {
  description = "Temporary GitHub Actions Runner IP for MySQL Access"
  type        = string
  default     = ""
}