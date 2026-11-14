mock_provider "aws" {
  override_during = plan
}

variables {
  aws_region           = "us-east-1"
  ssm_environment_name = "development"
}
override_resource {
  target          = aws_sqs_queue.orcamento_pagamento_entrada
  override_during = plan
  values = {
    arn = "arn:aws:sqs:us-east-1:123456789012:oficina-mecanica-orcamento-pagamento-development.fifo"
    url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-orcamento-pagamento-development.fifo"
  }
}
override_resource {
  target          = aws_sqs_queue.orcamento_pagamento_dlq
  override_during = plan
  values = {
    arn = "arn:aws:sqs:us-east-1:123456789012:oficina-mecanica-orcamento-pagamento-development-dlq.fifo"
    url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-orcamento-pagamento-development-dlq.fifo"
  }
}
run "parametros_fifo_e_redrive_conforme_rfc" {
  command = plan
  assert {
    condition = (aws_sqs_queue.orcamento_pagamento_entrada.fifo_queue && aws_sqs_queue.orcamento_pagamento_dlq.fifo_queue
      && !aws_sqs_queue.orcamento_pagamento_entrada.content_based_deduplication
      && aws_sqs_queue.orcamento_pagamento_entrada.sqs_managed_sse_enabled
      && aws_sqs_queue.orcamento_pagamento_dlq.sqs_managed_sse_enabled
      && aws_sqs_queue.orcamento_pagamento_entrada.visibility_timeout_seconds == 60
      && aws_sqs_queue.orcamento_pagamento_entrada.receive_wait_time_seconds == 20
      && aws_sqs_queue.orcamento_pagamento_entrada.message_retention_seconds == 345600
      && aws_sqs_queue.orcamento_pagamento_dlq.message_retention_seconds == 1209600
      && jsondecode(aws_sqs_queue.orcamento_pagamento_entrada.redrive_policy).maxReceiveCount == 5
    && jsondecode(aws_sqs_queue.orcamento_pagamento_entrada.redrive_policy).deadLetterTargetArn == aws_sqs_queue.orcamento_pagamento_dlq.arn)
    error_message = "Entrada e DLQ de Orçamento/Pagamento devem preservar os parâmetros da RFC-0007."
  }
  assert {
    condition = (jsondecode(aws_sqs_queue_redrive_allow_policy.orcamento_pagamento_dlq.redrive_allow_policy).redrivePermission == "byQueue"
    && jsondecode(aws_sqs_queue_redrive_allow_policy.orcamento_pagamento_dlq.redrive_allow_policy).sourceQueueArns == [aws_sqs_queue.orcamento_pagamento_entrada.arn])
    error_message = "A DLQ de Orçamento/Pagamento deve receber redrive somente da própria entrada."
  }
}
run "contratos_ssm_correspondem_aos_recursos_do_owner" {
  command = plan
  assert {
    condition = (aws_ssm_parameter.orcamento_pagamento_mensageria["entrada_queue_url"].value == aws_sqs_queue.orcamento_pagamento_entrada.url
      && aws_ssm_parameter.orcamento_pagamento_mensageria["entrada_queue_arn"].value == aws_sqs_queue.orcamento_pagamento_entrada.arn
      && aws_ssm_parameter.orcamento_pagamento_mensageria["dlq_url"].value == aws_sqs_queue.orcamento_pagamento_dlq.url
      && aws_ssm_parameter.orcamento_pagamento_mensageria["dlq_arn"].value == aws_sqs_queue.orcamento_pagamento_dlq.arn
    && alltrue([for parametro in aws_ssm_parameter.orcamento_pagamento_mensageria : parametro.type == "String"]))
    error_message = "Os contratos devem publicar referências dos recursos de Orçamento/Pagamento, sem credenciais."
  }
}
run "ambiente_parametriza_nomes_e_contratos" {
  command = plan
  variables {
    ssm_environment_name = "homologation"
  }
  assert {
    condition = (aws_sqs_queue.orcamento_pagamento_entrada.name == "oficina-mecanica-orcamento-pagamento-homologation.fifo"
      && aws_sqs_queue.orcamento_pagamento_dlq.name == "oficina-mecanica-orcamento-pagamento-homologation-dlq.fifo"
      && aws_ssm_parameter.orcamento_pagamento_mensageria["entrada_queue_url"].name == "/oficina-mecanica/homologation/orcamento-pagamento/entrada_queue_url"
    && aws_ssm_parameter.orcamento_pagamento_mensageria_status.name == "/oficina-mecanica/homologation/status/orcamento-pagamento")
    error_message = "Mensageria e contratos não podem apontar silenciosamente para development."
  }
}
