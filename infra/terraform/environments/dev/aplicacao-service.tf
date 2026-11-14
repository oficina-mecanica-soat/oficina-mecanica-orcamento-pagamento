resource "kubernetes_service_v1" "orcamento_pagamento" {
  count = var.aplicacao_habilitada ? 1 : 0

  metadata {
    name      = "oficina-mecanica-orcamento-pagamento"
    namespace = kubernetes_namespace_v1.orcamento_pagamento[0].metadata[0].name
  }

  spec {
    type     = "NodePort"
    selector = local.aplicacao_labels

    port {
      name        = "http"
      port        = 80
      target_port = 8080
      protocol    = "TCP"
      node_port   = var.aplicacao_node_port
    }
  }

  depends_on = [
    kubernetes_deployment_v1.orcamento_pagamento
  ]
}
