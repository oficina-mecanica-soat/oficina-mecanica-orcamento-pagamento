resource "kubernetes_secret_v1" "orcamento_pagamento" {
  count = var.aplicacao_habilitada ? 1 : 0
  metadata {
    name      = "oficina-mecanica-orcamento-pagamento-secret"
    namespace = kubernetes_namespace_v1.orcamento_pagamento[0].metadata[0].name
  }
  type = "Opaque"
  data = merge({
    Jwt__Secret                          = var.jwt_secret
    ConnectionStrings__DefaultConnection = var.db_connection_string
    }, var.mercado_pago_ativo ? {
    MercadoPago__AccessToken   = var.mercado_pago_access_token
    MercadoPago__WebhookSecret = var.mercado_pago_webhook_secret
  } : {})
}
