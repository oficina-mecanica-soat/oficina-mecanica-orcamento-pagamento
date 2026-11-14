resource "kubernetes_horizontal_pod_autoscaler_v2" "orcamento_pagamento" {
  count = var.aplicacao_habilitada ? 1 : 0

  metadata {
    name      = "oficina-mecanica-orcamento-pagamento-hpa"
    namespace = kubernetes_namespace_v1.orcamento_pagamento[0].metadata[0].name
  }

  spec {
    min_replicas = 1
    max_replicas = 5

    scale_target_ref {
      api_version = "apps/v1"
      kind        = "Deployment"
      name        = kubernetes_deployment_v1.orcamento_pagamento[0].metadata[0].name
    }

    metric {
      type = "Resource"

      resource {
        name = "cpu"

        target {
          type                = "Utilization"
          average_utilization = 70
        }
      }
    }

    metric {
      type = "Resource"

      resource {
        name = "memory"

        target {
          type                = "Utilization"
          average_utilization = 80
        }
      }
    }
  }

  depends_on = [
    kubernetes_deployment_v1.orcamento_pagamento
  ]
}
