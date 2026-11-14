output "mensageria" {
  description = "URLs e ARNs não secretos da entrada e DLQ de Orçamento/Pagamento."
  value       = local.orcamento_pagamento_messaging_contracts
}

output "aplicacao" {
  description = "Metadados não secretos do workload e do cluster para validação e recuperação."
  value = {
    habilitada   = var.aplicacao_habilitada
    cluster_name = var.aplicacao_cluster_name
    namespace    = "oficina-mecanica-orcamento-pagamento"
    deployment   = "oficina-mecanica-orcamento-pagamento"
    node_port    = var.aplicacao_node_port
    image_uri    = var.aplicacao_image_uri
  }
}
