resource "aws_ssm_parameter" "orcamento_pagamento_aplicacao_status" {
  count       = var.aplicacao_habilitada ? 1 : 0
  name        = "${local.ssm_base_path}/status/orcamento-pagamento-aplicacao"
  description = "Disponibilidade do workload; ready somente após rollout e health pela esteira."
  type        = "String"
  value       = "implantacao-pendente"
  tags        = local.common_tags
}
