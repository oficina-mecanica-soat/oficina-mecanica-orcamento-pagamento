locals {
  orcamento_pagamento_queue_prefix = "oficina-mecanica-orcamento-pagamento-${var.ssm_environment_name}"
  orcamento_pagamento_ssm_prefix   = "${local.ssm_base_path}/orcamento-pagamento"
  orcamento_pagamento_queue_tags = merge(local.common_tags, {
    Environment = var.ssm_environment_name
    Repository  = "oficina-mecanica-orcamento-pagamento"
  })
}

resource "aws_sqs_queue" "orcamento_pagamento_dlq" {
  name                        = "${local.orcamento_pagamento_queue_prefix}-dlq.fifo"
  fifo_queue                  = true
  content_based_deduplication = false
  message_retention_seconds   = 1209600
  sqs_managed_sse_enabled     = true
  tags                        = local.orcamento_pagamento_queue_tags
}

resource "aws_sqs_queue" "orcamento_pagamento_entrada" {
  name                        = "${local.orcamento_pagamento_queue_prefix}.fifo"
  fifo_queue                  = true
  content_based_deduplication = false
  visibility_timeout_seconds  = 60
  receive_wait_time_seconds   = 20
  message_retention_seconds   = 345600
  sqs_managed_sse_enabled     = true
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.orcamento_pagamento_dlq.arn
    maxReceiveCount     = 5
  })
  tags = local.orcamento_pagamento_queue_tags
}

resource "aws_sqs_queue_redrive_allow_policy" "orcamento_pagamento_dlq" {
  queue_url = aws_sqs_queue.orcamento_pagamento_dlq.url
  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.orcamento_pagamento_entrada.arn]
  })
}

locals {
  orcamento_pagamento_messaging_contracts = {
    entrada_queue_url = aws_sqs_queue.orcamento_pagamento_entrada.url
    entrada_queue_arn = aws_sqs_queue.orcamento_pagamento_entrada.arn
    dlq_url           = aws_sqs_queue.orcamento_pagamento_dlq.url
    dlq_arn           = aws_sqs_queue.orcamento_pagamento_dlq.arn
  }
}

resource "aws_ssm_parameter" "orcamento_pagamento_mensageria" {
  for_each    = local.orcamento_pagamento_messaging_contracts
  name        = "${local.orcamento_pagamento_ssm_prefix}/${each.key}"
  description = "Contrato não sensível da mensageria de Orçamento/Pagamento: ${each.key}."
  type        = "String"
  value       = each.value
  tags        = local.orcamento_pagamento_queue_tags
}

resource "aws_ssm_parameter" "orcamento_pagamento_mensageria_status" {
  name        = "${local.ssm_base_path}/status/orcamento-pagamento"
  description = "Disponibilidade da infraestrutura de mensageria de Orçamento/Pagamento; não comprova saúde do serviço ou processamento."
  type        = "String"
  value       = "ready"
  depends_on  = [aws_ssm_parameter.orcamento_pagamento_mensageria, aws_sqs_queue_redrive_allow_policy.orcamento_pagamento_dlq]
  tags        = local.orcamento_pagamento_queue_tags
}
