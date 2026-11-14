variable "aws_region" {
  description = "Região AWS da infraestrutura de Orçamento/Pagamento."
  type        = string
  default     = "us-east-1"
}

variable "ssm_environment_name" {
  description = "Nome do ambiente nos contratos SSM e nomes das filas."
  type        = string
  default     = "development"
  validation {
    condition     = contains(["development", "homologation", "production"], var.ssm_environment_name)
    error_message = "Use um ambiente documentado no Git Flow."
  }
}
