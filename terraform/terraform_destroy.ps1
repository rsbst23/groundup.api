Write-Host "Removing DynamoDB table from Terraform state..."
terraform state rm aws_dynamodb_table.terraform_lock

Write-Host "Destroying Terraform infrastructure..."
terraform destroy -auto-approve

Write-Host "Re-importing DynamoDB table..."
terraform import aws_dynamodb_table.terraform_lock terraform-lock