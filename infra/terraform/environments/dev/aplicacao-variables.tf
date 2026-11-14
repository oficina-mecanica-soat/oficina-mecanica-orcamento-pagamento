variable "aplicacao_habilitada" {
  description = "Implanta o workload somente pela esteira, após validar contratos próprios de banco e registry."
  type        = bool
  default     = false
}

variable "fluxo_distribuido_habilitado" {
  description = "Retira mutações HTTP que pertencem à coordenação da OS somente na janela de corte."
  type        = bool
  default     = false
  validation {
    condition     = !var.fluxo_distribuido_habilitado || var.processamento_remoto_habilitado
    error_message = "O corte distribuído exige os workers habilitados; não retirar as rotas antes do consumidor."
  }
}
variable "aplicacao_image_uri" {
  description = "Imagem imutável validada pela CI e publicada no ECR próprio."
  type        = string
  default     = ""
}
variable "aplicacao_cluster_name" {
  description = "Cluster compartilhado validado pela esteira e preservado no state para recuperação."
  type        = string
  default     = ""
}
variable "aplicacao_node_port" {
  description = "NodePort contratual exclusivo de Orçamento/Pagamento."
  type        = number
  default     = 30082
  validation {
    condition     = var.aplicacao_node_port >= 30000 && var.aplicacao_node_port <= 32767 && !contains([30080, 30081], var.aplicacao_node_port)
    error_message = "Use um NodePort válido e distinto de API/Execução."
  }
}
variable "db_connection_string" {
  description = "Conexão do banco e login exclusivos, resolvida pela esteira no Secrets Manager."
  type        = string
  sensitive   = true
  default     = ""
}
variable "jwt_secret" {
  description = "Chave JWT compatível com os emissores já aprovados."
  type        = string
  sensitive   = true
  default     = ""
}
variable "jwt_issuer" {
  description = "Issuer do contrato JWT vigente."
  type        = string
  default     = "oficina-mecanica-auth"
}
variable "jwt_audience" {
  description = "Audience do contrato JWT vigente."
  type        = string
  default     = "oficina-mecanica-api"
}
variable "processamento_remoto_habilitado" {
  description = "Ativação explícita dos workers após validar as duas filas."
  type        = bool
  default     = false
}
variable "conta_aws" {
  description = "Conta esperada pelos adaptadores SQS."
  type        = string
  default     = ""
}
variable "os_entrada_queue_url" {
  description = "Entrada FIFO de resultados OS publicada pelo owner API."
  type        = string
  default     = ""
}
variable "mercado_pago_ativo" {
  description = "Ativação explícita do provedor de teste; pode permanecer false no primeiro deploy."
  type        = bool
  default     = false
}
variable "mercado_pago_access_token" {
  description = "Credencial de teste do vendedor, recebida do GitHub Environment."
  type        = string
  sensitive   = true
  default     = ""
}
variable "mercado_pago_webhook_secret" {
  description = "Chave de assinatura das notificações, distinta do access token e JWT."
  type        = string
  sensitive   = true
  default     = ""
}
variable "mercado_pago_recebedor_id" {
  description = "Conta vendedora de teste esperada pelo adaptador."
  type        = string
  default     = ""
}
variable "mercado_pago_url_notificacao" {
  description = "URL HTTPS pública do Webhook no Gateway."
  type        = string
  default     = ""
}
