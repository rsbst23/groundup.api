output "ecs_sg_id" {
  description = "The ID of the ECS security group"
  value       = aws_security_group.ecs_sg.id
}

output "alb_sg_id" {
  description = "The ID of the ALB security group"
  value       = aws_security_group.alb_sg.id
}

output "mysql_sg_id" {
  description = "The ID of the MySQL security group"
  value       = aws_security_group.mysql_sg.id
}