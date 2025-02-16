output "ecs_cluster_name" {
  value = module.ecs_core.ecs_cluster_name
}

output "ecs_task_family_api" {
  value = module.ecs_services_api.api_task_definition_family
}

output "ecs_subnet_ids" {
  value = join(",", module.networking.subnet_ids)
}

output "ecs_security_group_ids" {
  value = join(",", [module.security.ecs_sg_id])
}

output "ecr_repository_url" {
  value = module.ecr.ecr_api_repository_url
}

output "debug_availability_zones" {
  value = var.availability_zones
}