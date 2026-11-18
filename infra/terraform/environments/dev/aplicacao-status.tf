resource "aws_ssm_parameter" "orcamento_pagamento_aplicacao_status" {
  count       = var.aplicacao_habilitada ? 1 : 0
  name        = "${local.ssm_base_path}/status/orcamento-pagamento-aplicacao"
  description = "Disponibilidade do workload; ready somente após rollout e health pela esteira."
  type        = "String"
  value       = "implantacao-pendente"
  tags        = local.common_tags
}

resource "aws_ssm_parameter" "orcamento_pagamento_fluxo_distribuido" {
  count       = var.aplicacao_habilitada ? 1 : 0
  name        = "${local.ssm_base_path}/orcamento-pagamento/fluxo_distribuido_habilitado"
  description = "Modo configurado do workload; consumir somente junto do status ready da aplicação."
  type        = "String"
  value       = tostring(var.fluxo_distribuido_habilitado)
  tags        = local.common_tags
  depends_on  = [kubernetes_config_map_v1.orcamento_pagamento]
}
