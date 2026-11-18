resource "kubernetes_namespace_v1" "orcamento_pagamento" {
  count = var.aplicacao_habilitada ? 1 : 0
  metadata {
    name = "oficina-mecanica-orcamento-pagamento"
  }
}
