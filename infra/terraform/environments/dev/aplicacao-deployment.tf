resource "kubernetes_deployment_v1" "orcamento_pagamento" {
  count = var.aplicacao_habilitada ? 1 : 0

  wait_for_rollout = false

  metadata {
    name      = "oficina-mecanica-orcamento-pagamento"
    namespace = kubernetes_namespace_v1.orcamento_pagamento[0].metadata[0].name
  }

  spec {
    replicas = 1

    selector {
      match_labels = local.aplicacao_labels
    }

    template {
      metadata {
        labels = merge(local.aplicacao_labels, local.aplicacao_datadog_labels, {
          "admission.datadoghq.com/enabled" = "true"
        })

        annotations = {
          "checksum/config"                                            = sha256(jsonencode(kubernetes_config_map_v1.orcamento_pagamento[0].data))
          "ad.datadoghq.com/oficina-mecanica-orcamento-pagamento.logs" = jsonencode([{ source = "csharp", service = "oficina-mecanica-orcamento-pagamento" }])
          "admission.datadoghq.com/dotnet-lib.version"                 = "3.50.0"
        }
      }

      spec {
        container {
          name              = "oficina-mecanica-orcamento-pagamento"
          image             = var.aplicacao_image_uri
          image_pull_policy = "Always"

          port {
            container_port = 8080
          }

          resources {
            requests = {
              cpu    = "100m"
              memory = "256Mi"
            }

            limits = {
              cpu    = "500m"
              memory = "512Mi"
            }
          }

          env_from {
            config_map_ref {
              name = kubernetes_config_map_v1.orcamento_pagamento[0].metadata[0].name
            }
          }

          env {
            name = "Jwt__Secret"

            value_from {
              secret_key_ref {
                name = kubernetes_secret_v1.orcamento_pagamento[0].metadata[0].name
                key  = "Jwt__Secret"
              }
            }
          }

          env {
            name = "ConnectionStrings__DefaultConnection"

            value_from {
              secret_key_ref {
                name = kubernetes_secret_v1.orcamento_pagamento[0].metadata[0].name
                key  = "ConnectionStrings__DefaultConnection"
              }
            }
          }

          dynamic "env" {
            for_each = var.mercado_pago_ativo ? toset(["MercadoPago__AccessToken", "MercadoPago__WebhookSecret"]) : toset([])
            content {
              name = env.value
              value_from {
                secret_key_ref {
                  name = kubernetes_secret_v1.orcamento_pagamento[0].metadata[0].name
                  key  = env.value
                }
              }
            }
          }

          startup_probe {
            http_get {
              path = "/api/health"
              port = 8080
            }

            failure_threshold = 30
            period_seconds    = 10
          }

          liveness_probe {
            http_get {
              path = "/api/health"
              port = 8080
            }

            initial_delay_seconds = 30
            period_seconds        = 15
            timeout_seconds       = 5
            failure_threshold     = 3
          }

          readiness_probe {
            http_get {
              path = "/api/health"
              port = 8080
            }

            initial_delay_seconds = 10
            period_seconds        = 10
            timeout_seconds       = 5
            failure_threshold     = 3
          }
        }
      }
    }
  }

  lifecycle {
    precondition {
      condition     = can(regex("^[0-9]{12}\\.dkr\\.ecr\\.[a-z0-9-]+\\.amazonaws\\.com/oficina-mecanica-orcamento-pagamento:sha-[0-9a-f]{40}$", var.aplicacao_image_uri))
      error_message = "Use somente o registry próprio e a tag imutável do commit validado."
    }
    precondition {
      condition     = length(var.jwt_secret) >= 32 && trimspace(var.db_connection_string) != ""
      error_message = "Workload exige chave JWT válida e conexão própria."
    }
    precondition {
      condition     = !var.processamento_remoto_habilitado || (can(regex("^[0-9]{12}$", var.conta_aws)) && var.os_entrada_queue_url == "https://sqs.${var.aws_region}.amazonaws.com/${var.conta_aws}/oficina-mecanica-api-${var.ssm_environment_name}.fifo")
      error_message = "Workers exigem conta e fila FIFO do owner OS no mesmo ambiente."
    }
    precondition {
      condition     = !var.mercado_pago_ativo || (length(var.mercado_pago_access_token) > 0 && length(var.mercado_pago_webhook_secret) > 0 && can(regex("^[1-9][0-9]*$", var.mercado_pago_recebedor_id)) && startswith(var.mercado_pago_url_notificacao, "https://"))
      error_message = "Mercado Pago exige configuração completa de teste."
    }
  }

  depends_on = [
    kubernetes_config_map_v1.orcamento_pagamento,
    kubernetes_secret_v1.orcamento_pagamento
  ]
}
