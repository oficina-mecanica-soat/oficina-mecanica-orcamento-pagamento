locals {
  ssm_base_path = "/oficina-mecanica/${var.ssm_environment_name}"
  common_tags = {
    Project     = "OficinaMecanica"
    Environment = var.ssm_environment_name
    ManagedBy   = "Terraform"
    Repository  = "oficina-mecanica-orcamento-pagamento"
  }
}
