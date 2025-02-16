resource "aws_ecr_repository" "api" {
  name         = "${var.ecr_repository_name}-api"
  force_delete = true

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name = var.ecr_repository_name
  }
}

resource "aws_ecr_repository" "ef_migrations" {
  name                 = "${var.ecr_repository_name}-ef-migrations"
  force_delete = true

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name = "${var.ecr_repository_name}-ef-migrations"
  }
}