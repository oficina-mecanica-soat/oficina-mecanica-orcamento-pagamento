locals {
  aplicacao_labels  = { app = "oficina-mecanica-orcamento-pagamento" }
  aplicacao_version = var.aplicacao_image_uri == "" ? "desabilitado" : element(reverse(split(":", var.aplicacao_image_uri)), 0)
  aplicacao_datadog_labels = {
    "tags.datadoghq.com/env"     = var.ssm_environment_name
    "tags.datadoghq.com/service" = "oficina-mecanica-orcamento-pagamento"
    "tags.datadoghq.com/version" = local.aplicacao_version
  }
}
