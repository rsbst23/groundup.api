output "alb_arn" {
  description = "ARN of the created Application Load Balancer"
  value       = aws_lb.main.arn
}

output "alb_dns_name" {
  description = "DNS name of the ALB"
  value       = aws_lb.main.dns_name
}

output "target_group_arn" {
  description = "ARN of the target group for ECS"
  value       = aws_lb_target_group.api_tg.arn
}

output "alb_listener_arn" {
  description = "The ARN of the ALB listener"
  value       = aws_lb_listener.main.arn
}

output "api_target_group_arn" {
  description = "The ARN of the target group for the API"
  value       = aws_lb_target_group.api_tg.arn
}
