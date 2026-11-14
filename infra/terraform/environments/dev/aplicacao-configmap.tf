resource "kubernetes_config_map_v1" "orcamento_pagamento" {
  count = var.aplicacao_habilitada ? 1 : 0
  metadata {
    name      = "oficina-mecanica-orcamento-pagamento-config"
    namespace = kubernetes_namespace_v1.orcamento_pagamento[0].metadata[0].name
  }
  data = {
    ASPNETCORE_ENVIRONMENT                   = "Staging"
    ASPNETCORE_URLS                          = "http://+:8080"
    Database__ApplyMigrationsOnStartup       = "true"
    Jwt__Issuer                              = var.jwt_issuer
    Jwt__Audience                            = var.jwt_audience
    ProcessamentoRemoto__Habilitado          = tostring(var.processamento_remoto_habilitado)
    ProcessamentoRemoto__ContaEsperada       = var.conta_aws
    ProcessamentoRemoto__Regiao              = var.aws_region
    ProcessamentoRemoto__FilaEntradaUrl      = aws_sqs_queue.orcamento_pagamento_entrada.url
    ProcessamentoRemoto__FilaResultadosOsUrl = var.os_entrada_queue_url
    MercadoPago__Ativo                       = tostring(var.mercado_pago_ativo)
    MercadoPago__RecebedorId                 = var.mercado_pago_recebedor_id
    MercadoPago__UrlNotificacao              = var.mercado_pago_url_notificacao
    DD_ENV                                   = var.ssm_environment_name
    DD_SERVICE                               = "oficina-mecanica-orcamento-pagamento"
    DD_VERSION                               = local.aplicacao_version
    DD_LOGS_INJECTION                        = "true"
    DD_TRACE_PROPAGATION_STYLE_INJECT        = "tracecontext"
    DD_TRACE_PROPAGATION_STYLE_EXTRACT       = "tracecontext"
    DD_HTTP_SERVER_TAG_QUERY_STRING          = "false"
  }
}
