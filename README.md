# 💳 Oficina Mecânica — Orçamento e Pagamento

Microsserviço de orçamento, decisão do cliente, cobrança e confirmação financeira. Mantém banco SQL Server, login, migrations, fila e workload próprios. OS coordena a Saga; Execução mantém o trabalho físico.

## 🏗️ Recursos provisionados por esta esteira

| Recurso próprio | Uso e integração |
| --- | --- |
| Namespace, Deployment, ConfigMap, Secret, Service NodePort 30082 e HPA | Workload de Orçamento/Pagamento no EKS compartilhado; imagem publicada no ECR próprio já criado pela plataforma |
| Entrada SQS FIFO, DLQ e política de redrive | Comandos da Saga de OS; os resultados voltam pela fila de OS |
| Parâmetros SSM de filas, modo distribuído e prontidão do workload | Publicar contratos consumidos pela API e pelo Gateway |
| Migrations do banco próprio | Criar/evoluir schemas e tabelas de orçamento, cobrança, Inbox/Outbox no startup configurado |

A [esteira RDS](https://github.com/oficina-mecanica-soat/oficina-mecanica-infra-rds) cria/prepara instância, banco lógico, login e secret de conexão; este repositório consome a credencial exclusiva. EKS, ECR e listener 8082 são da plataforma Kubernetes; rotas públicas e URL do Webhook são do Gateway. A configuração no painel Mercado Pago pertence à aplicação vendedora de teste, não é um recurso provisionado pelo Terraform. Fonte: [`infra/terraform/environments/dev`](infra/terraform/environments/dev).

<a id="arquitetura"></a>

## 🏗️ Arquitetura

Clean Architecture separa Domain, Application, Infrastructure e API. Snapshots preservam preços e quantidades da versão aprovada. Transação SQL confirma estado, Inbox e Outbox; `rowversion` e restrições únicas protegem concorrência e duplicatas. A integração Mercado Pago fica na Infrastructure e não acopla o Domain ao provedor.

<a id="executar-localmente"></a>

## 🚀 Executar localmente

Pré-requisitos: Docker com containers Linux; .NET SDK 10 para execução dos testes fora do container.

1. Copie `.env.example` para `.env`.
2. Preencha uma senha local forte para SQL Server e uma chave JWT de desenvolvimento com pelo menos 32 bytes. Não versione `.env`. Para aceitar tokens de outro ambiente local, os serviços precisam compartilhar chave, issuer e audience.
3. Execute:

```powershell
docker compose up --build -d
```

API: `http://localhost:5095`; Swagger: `http://localhost:5095/swagger`; saúde: `http://localhost:5095/api/health`. SQL local: `localhost,14335`, banco `OficinaMecanicaOrcamentoPagamento`.

O Compose usa SQL Developer e `sa` exclusivamente no desenvolvimento local. Aplica a migration inicial ao iniciar a API. Fora do Compose, `Database:ApplyMigrationsOnStartup` é `false`; não usar credencial master compartilhada no futuro RDS. O provisionamento de banco/usuário próprios e a implantação devem ocorrer pelas esteiras oficiais.

```powershell
docker compose down
```

Esse comando preserva o volume de dados local. Não use remoção de volumes se quiser conservar seus orçamentos.

<a id="adaptador-mercado-pago"></a>

## 💳 Adaptador Mercado Pago

A integração externa usa um `HttpClient` tipado na Infrastructure e contratos próprios na Application. DTOs e nomes da API Mercado Pago permanecem na Infrastructure. O Domain não depende do provedor.

| Variável local | Configuração .NET | Uso |
| --- | --- | --- |
| `MERCADO_PAGO_ATIVO` | `MercadoPago:Ativo` | `false` por padrão; ativação exige configuração completa |
| `MERCADO_PAGO_ACCESS_TOKEN` | `MercadoPago:AccessToken` | Token da conta vendedora de teste |
| `MERCADO_PAGO_WEBHOOK_SECRET` | `MercadoPago:WebhookSecret` | Chave de assinatura das notificações, distinta do token de acesso |
| `MERCADO_PAGO_RECEBEDOR_ID` | `MercadoPago:RecebedorId` | ID da conta vendedora esperada nas respostas |
| `MERCADO_PAGO_URL_NOTIFICACAO` | `MercadoPago:UrlNotificacao` | Endereço HTTPS público configurado para notificações |

O Compose encaminha essas variáveis de `.env`; fora dele, usar as chaves equivalentes com `__` nas variáveis de ambiente. Configuração incompleta com ativação ligada falha na inicialização. As credenciais ficam fora do Git.

O adaptador oferece criação de preferência Checkout Pro, reconciliação por referência e consulta de pagamento. Retorna somente endereço de sandbox e exige BRL, o recebedor configurado, referência e valor compatíveis. Pagamentos com `live_mode=false` seguem a validação de teste; quando o provedor retorna `live_mode=true` para contas de teste, exige também consulta autenticada a `GET /users/me`, com o mesmo recebedor e a tag exata `test_user`. Conta real, identidade divergente, tag ausente ou falha nessa consulta não permitem confirmação. HTTP não possui repetição automática de criação: uma resposta incerta exige consulta por referência. A intenção/chave deve ser persistida pelo caso de uso antes da chamada; resultado sem correspondência ou ambíguo não autoriza outra criação automática.

O validador HMAC autentica assinatura, requisição e identificador do recurso conforme o manifesto do provedor. Isso não confirma pagamento nem deduplica efeitos: o caso de uso precisa consultar o provedor, conferir o snapshot e confirmar estado/Inbox/Outbox na transação local. Timestamp participa da assinatura; este componente não impõe janela de expiração, e a proteção contra reentrega pertence à idempotência do fluxo.

Essa configuração compõe os adaptadores; as rotas de consulta e Webhook são descritas abaixo. A confirmação depende da consulta e validação do estado no provedor. Referências: [preferências](https://www.mercadopago.com.br/developers/pt/reference/online-payments/checkout-pro-preferences/overview) e [notificações](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-preferences/payment-notifications).

<a id="contrato-http-local"></a>

## 📡 Contrato HTTP local

Todas as rotas abaixo exigem JWT válido; este serviço não emite tokens. Defaults compartilhados: issuer `oficina-mecanica-auth` e audience `oficina-mecanica-api`, configuráveis em `Jwt`.

| Método e rota | Permissão | Resultado |
| --- | --- | --- |
| `POST /api/v1/orcamentos` | Administrador, Atendente | `201`, snapshot persistido |
| `GET /api/v1/orcamentos/{id}` | Administrador, Atendente, Cliente | `200`, orçamento |
| `POST /api/v1/orcamentos/{id}/decisao` | Administrador, Atendente, Cliente | `202`, decisão e resultado persistidos |
| `POST /api/v1/orcamentos/{id}/cancelar` | Administrador, Atendente | `202`, cancelamento e resultado persistidos |

Cliente é limitado ao próprio `cliente_id` do JWT. Identificador ausente/inválido retorna `403`; orçamento de outro cliente retorna `404`. `202` comprova o commit local, sem afirmar conclusão da Saga ou entrega da mensagem.

Decisão: `{"versao":1,"decisao":1,"operacaoId":"UUID"}`; `1` aprova e `2` recusa. No modo distribuído, geração e cancelamento são coordenados pela Saga; não usar as rotas administrativas para contornar esse fluxo.

<a id="pagamentos"></a>

## 💳 Pagamentos

A cobrança é solicitada internamente pelo comando `CriarCobranca`, após a finalização física confirmada pela Saga de OS. O valor e o cliente são obtidos do orçamento aprovado no banco deste serviço. Não existe endpoint público para criar cobranças.

- `GET /api/v1/pagamentos/{pagamentoId}`: consulta local com JWT, disponível para administrador/atendente ou para o cliente proprietário. Um cliente não consulta pagamentos de outro cliente. Após confirmação, a resposta deixa de oferecer o endereço do checkout.
- `POST /api/v1/pagamentos/notificacoes/mercado-pago?data.id={identificador}`: Webhook sem JWT, autenticado por `x-signature` e `x-request-id`. Configure notificações do tipo `payment`; o `data.id` do corpo precisa corresponder à query. O estado financeiro é consultado no provedor, nunca aceito do corpo da notificação. O sucesso retorna 200 sem dados financeiros; indisponibilidade do provedor retorna 503 para permitir reentrega.

Configure `MercadoPago:UrlNotificacao` com o endereço HTTPS público desse Webhook. Os demais parâmetros e o bloqueio padrão da integração estão descritos na configuração do Mercado Pago deste README. Migrations pertencem exclusivamente ao banco Orçamento/Pagamento.

A intenção de cobrança é confirmada no SQL antes do POST externo. A referência externa é o UUID local do pagamento. Após resultado incerto, repetições pesquisam a preferência pela referência; não repetem automaticamente a criação. Uma intenção em `CriacaoEmAndamento` sem preferência encontrada exige conciliação operacional, inclusive se houve interrupção entre confirmar a intenção e enviar o POST. Não reabrir a criação nem alterar o banco manualmente para forçar uma segunda tentativa.

Somente a aprovação confirmada pelo provedor gera `PagamentoConfirmado` junto da alteração local, via Outbox. Recusa não impede outra tentativa aprovada; eventos atrasados não regridem a confirmação. Estorno, contestação e estados desconhecidos exigem conciliação manual. Um orçamento com cobrança solicitada não pode ser compensado por cancelamento.

<a id="processamento-de-mensagens"></a>

## 📨 Processamento de mensagens

`ProcessamentoRemoto:Habilitado` é `false` por padrão. Nesse modo, o host não cria clientes AWS nem inicia workers. Os processadores continuam disponíveis para testes e composição local por arquivos.

O workload remoto recebe da esteira oficial `ProcessamentoRemoto:ContaEsperada`, `Regiao`, `FilaEntradaUrl` e `FilaResultadosOsUrl`. As duas filas devem ser FIFO, distintas e pertencer à conta/região informadas. A identidade é obtida explicitamente da `LabRole` do workload e conferida por STS; não usa credenciais de ambiente ou perfis locais como fallback. A configuração remota deve ser ativada somente na implantação autorizada pela esteira.

O consumidor usa long polling de 20 segundos, uma mensagem por vez e visibilidade de 60 segundos, renovada a cada 20 segundos. Confere os metadados FIFO contra o envelope e exclui a entrada somente após o caso de uso concluir. Falhas de contrato, persistência, conciliação ou renovação preservam a possibilidade de reentrega. Mensagens não reconhecidas não são confirmadas; redrive/DLQ pertencem à infraestrutura da fila.

O publicador trabalha em um loop separado, para que a espera do consumidor não impeça o envio da Outbox. As tentativas preservam `mensagemId` e o agrupamento por OS. O SDK usa timeout de 30 segundos e não repete requisições internamente; Inbox/Outbox e o worker controlam a recuperação. Os testes com SDK substituído e SQL real não comprovam FIFO/DLQ/IAM ou entrega em AWS.

<a id="validar"></a>

## 🧪 Validar

Na [CI de referência do código entregue](https://github.com/oficina-mecanica-soat/oficina-mecanica-orcamento-pagamento/actions/runs/43952148323), foram aprovados **305 testes**, sem falhas ou ignorados, com **94,04% de cobertura global de linhas** (2.100 de 2.233 linhas). Os relatórios estão no artefato `test-and-coverage-results`. O gate exige 90%; uma CI somente documental não representa nova medição.

A CI em `.github/workflows/ci.yml` reaproveita os jobs da API principal: detecção de escopo, Git Flow/estrutura, build, formatação, testes SQL com Docker, cobertura mínima de 90%, imagem Docker sem push e quality gate obrigatório. A imagem e a identificação da árvore validada ficam em artefato, preservando a referência para o futuro CD. Testes ignorados bloqueiam o aceite.

O CI valida aplicação, cobertura, imagem e Terraform. O CD publica a imagem aprovada, aplica o plano salvo e verifica rollout/health.

```powershell
dotnet restore OficinaMecanica.OrcamentoPagamento.sln
dotnet build OficinaMecanica.OrcamentoPagamento.sln --configuration Release --no-restore
dotnet test OficinaMecanica.OrcamentoPagamento.sln --configuration Release --no-build --settings tests/sonarqube.runsettings --collect:"XPlat Code Coverage"
dotnet format OficinaMecanica.OrcamentoPagamento.sln --verify-no-changes --no-restore
```

Os testes de integração exigem Docker e criam SQL Server descartável com Testcontainers. Validam migrations, persistência real, rollback da transação, concorrência, idempotência, JWT, permissões e restrição por cliente. Não precisam de AWS nem de credenciais reais.

<a id="configuração-de-development"></a>

## ⚙️ Configuração de development

| Configuração do Environment development | Uso |
| --- | --- |
| `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN` | Credenciais temporárias da janela AWS Academy |
| `TERRAFORM_STATE_KEY` | Secret exclusivo com 64 caracteres hexadecimais, correspondente a 256 bits; proteger a cópia de recuperação |
| `JWT_SECRET` | Mesma chave dos emissores autorizados; não gerar chave diferente por serviço |
| `JWT_ISSUER`, `JWT_AUDIENCE` | Variables do contrato compartilhado; defaults oficina-mecanica-auth e oficina-mecanica-api |
| `FLUXO_DISTRIBUIDO_ENABLED` | Variable false por padrão; true exige workers habilitados e bloqueia geração/cancelamento HTTP coordenados pela OS |
| `PROCESSAMENTO_REMOTO_ENABLED` | Variable false por padrão; true exige validar a fila FIFO real de resultados OS |
| `MERCADO_PAGO_ATIVO` | Variable false por padrão; ativação exige os secrets e Variables da tabela Mercado Pago acima |

O banco `OficinaMecanicaOrcamentoPagamento` e o login `oficina_orcamento_pagamento` são preparados pela esteira RDS, com acesso restrito ao banco próprio. O CD obtém o secret exclusivo; não utiliza a credencial master ou da API. Workload no EKS usa NodePort 30082 e listener privado 8082. HPA acadêmico de uma a duas réplicas; não representa dimensionamento de produção.

A primeira implantação mantém Mercado Pago desligado. Após existir a URL pública do Gateway, configurar notificações `payment` e assinatura na aplicação vendedora de teste; então reaplicar a configuração pela esteira. Health não comprova disponibilidade do pagamento.

Logs HTTP usam a mesma convenção dos demais serviços. Outbox propaga o `traceparent` validado, e logs incluem os IDs de correlação. Consulte a [RFC-0006 de observabilidade](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/blob/develop/docs/architecture/rfcs/rfc-0006-observabilidade-centralizada-com-datadog.md).

<a id="operação-e-documentação"></a>

## 🧭 Operação e documentação

- [Visão geral, arquitetura e ordem das esteiras](https://github.com/oficina-mecanica-soat/oficina-mecanica-api#deploy-aws).
- [Configuração, primeira aplicação e recuperação após Reset](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/blob/develop/docs/deploy/deploy-aws.md).
- [Collection consolidada e ambientes Local/Development](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/blob/develop/docs/evidencias/postman.md).

Todo provisionamento, atualização e destroy na AWS ocorre pelas esteiras oficiais. O controle é `infra/terraform/environments/dev/terraform-action.env`; destroy exige alteração dedicada e verificação dos consumidores. `AWS_DEPLOY_ENABLED` autoriza a janela real, e `AUTO_PR_ENABLED` controla somente a abertura de promoções. Alterar uma Variable não dispara o CD.

Um ambiente disponível pode ser testado sem destroy. Sem estado compatível, alguns owners exigem primeiro uma recuperação por destroy dedicado com inventário vazio comprovado. Não apague recursos ou caches para contornar o bloqueio; consulte o guia antes de iniciar uma conta ou retomar um laboratório.

Fluxo Git: branch de trabalho → PR para `develop` → `release` → `main`. O CD físico ocorre em development; release e production registram promoções lógicas. PRs e merges permanecem sujeitos aos checks e à revisão. Mudanças somente em README/docs não exigem deploy físico pelos detectores atuais.
