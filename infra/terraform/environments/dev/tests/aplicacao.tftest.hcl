mock_provider "aws" {
  mock_data "aws_eks_cluster" {
    defaults = {
      endpoint              = "https://127.0.0.1"
      certificate_authority = [{ data = "dGVzdGU=" }]
    }
  }
}
mock_provider "kubernetes" {}
override_resource {
  target          = aws_sqs_queue.orcamento_pagamento_entrada
  override_during = plan
  values = {
    url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-orcamento-pagamento-development.fifo"
    arn = "arn:aws:sqs:us-east-1:123456789012:oficina-mecanica-orcamento-pagamento-development.fifo"
  }
}
variables {
  aws_region             = "us-east-1"
  aplicacao_habilitada   = true
  aplicacao_cluster_name = "oficina-mecanica-eks-dev"
  aplicacao_image_uri    = "123456789012.dkr.ecr.us-east-1.amazonaws.com/oficina-mecanica-orcamento-pagamento:sha-0123456789abcdef0123456789abcdef01234567"
  db_connection_string   = "conexao-ficticia-somente-teste"
  jwt_secret             = "chave-ficticia-de-teste-com-mais-de-32-caracteres"
}
run "workload_preserva_secret_probes_datadog_e_porta_exclusiva" {
  command = plan
  assert {
    condition = (
      kubernetes_deployment_v1.orcamento_pagamento[0].spec[0].template[0].spec[0].container[0].image == var.aplicacao_image_uri &&
      kubernetes_service_v1.orcamento_pagamento[0].spec[0].type == "NodePort" &&
      kubernetes_service_v1.orcamento_pagamento[0].spec[0].port[0].node_port == 30082 &&
      kubernetes_deployment_v1.orcamento_pagamento[0].spec[0].template[0].spec[0].container[0].readiness_probe[0].http_get[0].path == "/api/health" &&
      kubernetes_deployment_v1.orcamento_pagamento[0].spec[0].template[0].metadata[0].annotations["admission.datadoghq.com/dotnet-lib.version"] == "3.50.0"
    )
    error_message = "Workload deve preservar imagem validada, probes e instrumentação da API."
  }
  assert {
    condition = (
      !contains(keys(kubernetes_config_map_v1.orcamento_pagamento[0].data), "Jwt__Secret") &&
      !contains(keys(kubernetes_config_map_v1.orcamento_pagamento[0].data), "ConnectionStrings__DefaultConnection") &&
      !contains(keys(kubernetes_secret_v1.orcamento_pagamento[0].data), "MercadoPago__AccessToken") &&
      kubernetes_config_map_v1.orcamento_pagamento[0].data["ProcessamentoRemoto__Habilitado"] == "false" &&
      aws_ssm_parameter.orcamento_pagamento_aplicacao_status[0].value == "implantacao-pendente"
    )
    error_message = "Segredos ficam fora do ConfigMap e ativação/disponibilidade precisam de prova explícita."
  }
}
run "ativacao_remota_usa_conta_e_resultados_do_owner_os" {
  command = plan
  variables {
    processamento_remoto_habilitado = true
    conta_aws                       = "123456789012"
    os_entrada_queue_url            = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-api-development.fifo"
  }
  assert {
    condition     = kubernetes_config_map_v1.orcamento_pagamento[0].data["ProcessamentoRemoto__FilaResultadosOsUrl"] == var.os_entrada_queue_url
    error_message = "Resultados devem ir à entrada da OS."
  }
}

run "corte_publica_modo_do_workload_e_reinicia_pods_por_configuracao" {
  command = plan
  variables {
    fluxo_distribuido_habilitado    = true
    processamento_remoto_habilitado = true
    conta_aws                       = "123456789012"
    os_entrada_queue_url            = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-api-development.fifo"
  }
  assert {
    condition = (
      kubernetes_config_map_v1.orcamento_pagamento[0].data["Integracoes__FluxoDistribuido__Habilitado"] == "true" &&
      aws_ssm_parameter.orcamento_pagamento_fluxo_distribuido[0].value == "true" &&
      aws_ssm_parameter.orcamento_pagamento_fluxo_distribuido[0].name == "/oficina-mecanica/development/orcamento-pagamento/fluxo_distribuido_habilitado" &&
      kubernetes_deployment_v1.orcamento_pagamento[0].spec[0].template[0].metadata[0].annotations["checksum/config"] == sha256(jsonencode(kubernetes_config_map_v1.orcamento_pagamento[0].data))
    )
    error_message = "Corte deve publicar a flag aplicada e provocar rollout quando a configuração mudar."
  }
}

run "corte_sem_worker_nao_retira_rotas" {
  command = plan
  variables { fluxo_distribuido_habilitado = true }
  expect_failures = [var.fluxo_distribuido_habilitado]
}
run "fila_alheia_nao_ativa_workers" {
  command = plan
  variables {
    processamento_remoto_habilitado = true
    conta_aws                       = "123456789012"
    os_entrada_queue_url            = "https://sqs.us-east-1.amazonaws.com/999999999999/oficina-mecanica-api-development.fifo"
  }
  expect_failures = [kubernetes_deployment_v1.orcamento_pagamento]
}
run "mercado_pago_nao_ativa_sem_credenciais" {
  command = plan
  variables { mercado_pago_ativo = true }
  expect_failures = [kubernetes_deployment_v1.orcamento_pagamento]
}
run "aplicacao_desabilitada_nao_cria_recursos_kubernetes" {
  command = plan
  variables { aplicacao_habilitada = false }
  assert {
    condition     = length(kubernetes_namespace_v1.orcamento_pagamento) == 0 && length(kubernetes_deployment_v1.orcamento_pagamento) == 0 && length(kubernetes_secret_v1.orcamento_pagamento) == 0
    error_message = "Modo desabilitado deve preservar somente mensageria."
  }
}
