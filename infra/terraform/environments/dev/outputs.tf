output "mensageria" {
  description = "URLs e ARNs não secretos da entrada e DLQ de Orçamento/Pagamento."
  value       = local.orcamento_pagamento_messaging_contracts
}
