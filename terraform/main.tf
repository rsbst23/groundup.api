resource "aws_ecs_cluster" "ecs_cluster" {
  name = var.ecs_cluster_name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = {
    Name = var.ecs_cluster_name
  }
}

module "networking" {
  source             = "./modules/networking"
  project_name       = var.project_name
  vpc_cidr           = var.vpc_cidr
  availability_zones = var.availability_zones
}

module "logging" {
  source         = "./modules/logging"
  project_name   = var.project_name
  log_group_name = var.log_group_name
}

module "security" {
  source              = "./modules/security"
  project_name        = var.project_name
  vpc_id              = module.networking.vpc_id
  github_runner_ip    = var.github_runner_ip
}

module "ecr" {
  source              = "./modules/ecr"
  ecr_repository_name = var.ecr_repository_name
}

module "alb" {
  source       = "./modules/alb"
  project_name = var.project_name
  vpc_id       = module.networking.vpc_id
  subnet_ids   = module.networking.subnet_ids
  alb_sg_id    = module.security.alb_sg_id
}

module "service_discovery" {
  source       = "./modules/service_discovery"
  project_name = var.project_name
  vpc_id       = module.networking.vpc_id
}

module "ecs_core" {
  source              = "./modules/ecs/core"
  project_name        = var.project_name
  ecs_cluster_name    = var.ecs_cluster_name
  secrets_manager_arn = var.mysql_secrets_arn
  cloudwatch_logs_arn = module.logging.cloudwatch_logs_arn
}

module "ecs_services_mysql" {
  source                      = "./modules/ecs/services/mysql"
  project_name                = var.project_name
  ecs_cluster_id              = module.ecs_core.ecs_cluster_id
  ecs_task_execution_role_arn = module.ecs_core.ecs_task_execution_role_arn
  mysql_sg_id                 = module.security.mysql_sg_id
  subnets                     = module.networking.subnet_ids
  mysql_service_discovery_arn = module.service_discovery.mysql_service_discovery_arn
  cloudwatch_logs_name        = var.log_group_name
  aws_region                  = var.aws_region
  mysql_secrets_arn           = var.mysql_secrets_arn
  ecs_task_cpu                = var.ecs_task_cpu
  ecs_task_memory             = var.ecs_task_memory
  desired_count               = var.mysql_desired_count

  depends_on = [module.ecr]
}

# Deploy API service separately after MySQL is up
module "ecs_services_api" {
  source                    = "./modules/ecs/services/api"
  project_name              = var.project_name
  ecs_cluster_id            = module.ecs_core.ecs_cluster_id
  ecs_task_execution_role_arn = module.ecs_core.ecs_task_execution_role_arn
  ecs_sg_id                 = module.security.ecs_sg_id
  subnets                   = module.networking.subnet_ids
  api_service_discovery_arn = module.service_discovery.api_service_discovery_arn
  api_target_group_arn      = module.alb.api_target_group_arn
  alb_listener_arn          = module.alb.alb_listener_arn
  ecr_api_repository_url    = module.ecr.ecr_api_repository_url
  cloudwatch_logs_name      = var.log_group_name
  aws_region                = var.aws_region
  mysql_secrets_arn         = var.mysql_secrets_arn
  ecs_task_cpu              = var.ecs_task_cpu
  ecs_task_memory           = var.ecs_task_memory
  desired_count             = var.api_desired_count

  depends_on = [module.ecr, module.ecs_services_mysql]
}

# Deploy EF Migrations task separately after MySQL is up
module "ecs_services_ef_migrations" {
  source                    = "./modules/ecs/services/ef-migrations"
  project_name              = var.project_name
  ecs_cluster_id            = module.ecs_core.ecs_cluster_id
  ecs_task_execution_role_arn = module.ecs_core.ecs_task_execution_role_arn
  ecs_sg_id                 = module.security.ecs_sg_id
  subnets                   = module.networking.subnet_ids
  ecr_ef_migrations_repository_url = module.ecr.ecr_ef_migrations_repository_url
  cloudwatch_logs_name      = var.log_group_name
  aws_region                = var.aws_region
  mysql_secrets_arn         = var.mysql_secrets_arn
  ecs_task_cpu              = var.ecs_task_cpu
  ecs_task_memory           = var.ecs_task_memory

  depends_on = [module.ecr, module.ecs_services_mysql]
}