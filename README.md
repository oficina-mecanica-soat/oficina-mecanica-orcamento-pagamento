# Orçamento/Pagamento — Oficina Mecânica

Serviço responsável por orçamentos versionados e, na evolução prevista, cobrança. A implementação em revisão gera, consulta, aprova, recusa e cancela orçamentos em SQL Server próprio. O código é disponibilizado por PR para `develop`; não houve implantação AWS.

## Arquitetura e referências

As decisões continuam centralizadas na [API principal](https://github.com/oficina-mecanica-soat/oficina-mecanica-api): ADR-0024, ADR-0025, ADR-0026, RFC-0007 e `docs/projeto/backlog-tecnico.md` (F4-07/F4-08). Consulte também o [AGENTS.md](AGENTS.md).

O serviço preserva a organização da API principal: `Domain`, `Application`, `Infrastructure` e `API`; funcionalidades agrupadas por contexto; Request/Response/Validator/UseCase; `Result<T>` e erros tipados; AutoMapper; FluentValidation; DI explícita; autenticação JWT; tratamento centralizado de exceções e logs estruturados. As configurações de JWT, Swagger, correlação, respostas HTTP e observabilidade foram adaptadas das implementações existentes. Os testes seguem Factories, AAA, xUnit e FluentAssertions.

Não existem referências de projeto, consultas SQL, entidades compartilhadas ou migrations da API principal. Os identificadores externos são referências de negócio, sem chaves estrangeiras entre bancos.

## Comportamento implementado

- Cada orçamento pertence a uma OS, um cliente e uma versão. Serviços e peças mantêm identificadores, descrições, quantidades e preços congelados; uma nova versão não altera a anterior. A mesma peça pode aparecer em linhas com preços distintos, preservando os lançamentos da OS sem média ou agrupamento. A chave técnica dessas linhas fica somente na persistência.
- A geração repetida com o mesmo snapshot e Saga retorna o orçamento existente. Conteúdo ou Saga divergentes para a mesma OS/versão retornam conflito.
- Aprovação e recusa só ocorrem enquanto o orçamento aguarda aprovação. Repetir a mesma decisão não produz outro resultado. Cancelar preserva o snapshot e eventual decisão anterior; repetir o cancelamento exige o mesmo motivo.
- Orçamento, contexto de integração e resultado na outbox são gravados na mesma transação SQL. `rowversion` protege alterações concorrentes; índices únicos impedem versões e resultados duplicados.
- Resultados `OrcamentoGerado`, `OrcamentoDecidido` e `OrcamentoCancelado` ficam persistidos no envelope v1 do RFC-0007, com Saga, correlação, causação e traceparent validado. O publicador local mantém corpo/ID após falhas e usa lease e backoff. **Ainda não há adaptador ou consumo SQS ativo.**
- O processador de comandos confirma recebimento, efeito e outbox em uma transação SQL. Reentregas compatíveis não duplicam efeitos; mesma identidade com conteúdo divergente é rejeitada. Cancelamento anterior à geração deixa um registro persistente que impede reabrir aquela versão, inclusive pelo caso de uso HTTP.

O contexto da Saga é metadado de persistência/integração, sem dependência tecnológica no Domain. O total usa `decimal(18,2)`; valores inválidos, identificadores de serviço repetidos e totais que excedam esse limite são rejeitados antes da persistência.

## Executar localmente

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

## Adaptador Mercado Pago

A integração externa usa um `HttpClient` tipado na Infrastructure e contratos próprios na Application. DTOs e nomes da API Mercado Pago permanecem na Infrastructure. O Domain não depende do provedor.

| Variável local | Configuração .NET | Uso |
| --- | --- | --- |
| `MERCADO_PAGO_ATIVO` | `MercadoPago:Ativo` | `false` por padrão; ativação exige configuração completa |
| `MERCADO_PAGO_ACCESS_TOKEN` | `MercadoPago:AccessToken` | Token da conta vendedora de teste |
| `MERCADO_PAGO_WEBHOOK_SECRET` | `MercadoPago:WebhookSecret` | Chave de assinatura das notificações, distinta do token de acesso |
| `MERCADO_PAGO_RECEBEDOR_ID` | `MercadoPago:RecebedorId` | ID da conta vendedora esperada nas respostas |
| `MERCADO_PAGO_URL_NOTIFICACAO` | `MercadoPago:UrlNotificacao` | Endereço HTTPS público configurado para notificações |

O Compose encaminha essas variáveis de `.env`; fora dele, usar as chaves equivalentes com `__` nas variáveis de ambiente. Configuração incompleta com ativação ligada falha na inicialização. As credenciais ficam fora do Git.

O adaptador oferece criação de preferência Checkout Pro, reconciliação por referência e consulta de pagamento. Retorna somente endereço de sandbox e rejeita pagamentos com `live_mode=true`, moeda diferente de BRL ou outro recebedor. HTTP não possui repetição automática de criação: uma resposta incerta exige consulta por referência. A intenção/chave deve ser persistida pelo caso de uso antes da chamada; resultado sem correspondência ou ambíguo não autoriza outra criação automática.

O validador HMAC autentica assinatura, requisição e identificador do recurso conforme o manifesto do provedor. Isso não confirma pagamento nem deduplica efeitos: o caso de uso precisa consultar o provedor, conferir o snapshot e confirmar estado/Inbox/Outbox na transação local. Timestamp participa da assinatura; este componente não impõe janela de expiração, e a proteção contra reentrega pertence à idempotência do fluxo.

Essa configuração compõe os adaptadores; não expõe rotas de cobrança/webhook nem registra pagamento automaticamente. Referências: [preferências](https://www.mercadopago.com.br/developers/pt/reference/online-payments/checkout-pro-preferences/overview) e [notificações](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-preferences/payment-notifications).

## Contrato HTTP local

Todas as rotas abaixo exigem JWT válido; este serviço não emite tokens. Defaults compartilhados: issuer `oficina-mecanica-auth` e audience `oficina-mecanica-api`, configuráveis em `Jwt`.

| Método e rota | Permissão | Resultado |
| --- | --- | --- |
| `POST /api/v1/orcamentos` | Administrador, Atendente | `201`, snapshot persistido |
| `GET /api/v1/orcamentos/{id}` | Administrador, Atendente, Cliente | `200`, orçamento |
| `POST /api/v1/orcamentos/{id}/decisao` | Administrador, Atendente, Cliente | `202`, decisão e resultado persistidos |
| `POST /api/v1/orcamentos/{id}/cancelar` | Administrador, Atendente | `202`, cancelamento e resultado persistidos |

Cliente é limitado ao próprio `cliente_id` do JWT. Identificador ausente/inválido retorna `403`; orçamento de outro cliente retorna `404`. `202` comprova o commit local, sem afirmar conclusão da Saga ou entrega da mensagem.

Exemplo de geração administrativa; substitua os UUIDs pelos identificadores da operação:

```json
{
  "ordemServicoId": "11111111-1111-1111-1111-111111111111",
  "clienteId": "22222222-2222-2222-2222-222222222222",
  "versao": 1,
  "contexto": {
    "sagaId": "33333333-3333-3333-3333-333333333333",
    "correlationId": "os-validacao-local",
    "mensagemId": "44444444-4444-4444-4444-444444444444"
  },
  "servicos": [
    {
      "servicoId": "55555555-5555-5555-5555-555555555555",
      "servicoCatalogoId": "66666666-6666-6666-6666-666666666666",
      "descricao": "Troca de óleo",
      "valor": 150.00
    }
  ],
  "pecasInsumos": [
    {
      "pecaInsumoCatalogoId": "77777777-7777-7777-7777-777777777777",
      "descricao": "Óleo lubrificante",
      "quantidade": 2,
      "valorUnitario": 45.00
    }
  ]
}
```

Decisão: `{"versao":1,"decisao":1,"operacaoId":"88888888-8888-8888-8888-888888888888"}`; `1` aprova e `2` recusa. Cancelamento: `{"versao":1,"operacaoId":"99999999-9999-9999-9999-999999999999","motivo":"Solicitação cancelada"}`. O cliente não informa sua identidade no corpo da decisão.

## Pagamentos

A cobrança é solicitada internamente pelo comando `CriarCobranca`, após a finalização física confirmada pela Saga de OS. O valor e o cliente são obtidos do orçamento aprovado no banco deste serviço. Não existe endpoint público para criar cobranças.

- `GET /api/v1/pagamentos/{pagamentoId}`: consulta local com JWT, disponível para administrador/atendente ou para o cliente proprietário. Um cliente não consulta pagamentos de outro cliente. Após confirmação, a resposta deixa de oferecer o endereço do checkout.
- `POST /api/v1/pagamentos/notificacoes/mercado-pago?data.id={identificador}`: Webhook sem JWT, autenticado por `x-signature` e `x-request-id`. Configure notificações do tipo `payment`; o `data.id` do corpo precisa corresponder à query. O estado financeiro é consultado no provedor, nunca aceito do corpo da notificação. O sucesso retorna 200 sem dados financeiros; indisponibilidade do provedor retorna 503 para permitir reentrega.

Configure `MercadoPago:UrlNotificacao` com o endereço HTTPS público desse Webhook. Os demais parâmetros e o bloqueio padrão da integração estão descritos na configuração do Mercado Pago deste README. Migrations pertencem exclusivamente ao banco Orçamento/Pagamento.

A intenção de cobrança é confirmada no SQL antes do POST externo. A referência externa é o UUID local do pagamento. Após resultado incerto, repetições pesquisam a preferência pela referência; não repetem automaticamente a criação. Uma intenção em `CriacaoEmAndamento` sem preferência encontrada exige conciliação operacional, inclusive se houve interrupção entre confirmar a intenção e enviar o POST. Não reabrir a criação nem alterar o banco manualmente para forçar uma segunda tentativa.

Somente a aprovação confirmada pelo provedor gera `PagamentoConfirmado` junto da alteração local, via Outbox. Recusa não impede outra tentativa aprovada; eventos atrasados não regridem a confirmação. Estorno, contestação e estados desconhecidos exigem conciliação manual. Um orçamento com cobrança solicitada não pode ser compensado por cancelamento.

## Processamento de mensagens

`ProcessamentoRemoto:Habilitado` é `false` por padrão. Nesse modo, o host não cria clientes AWS nem inicia workers. Os processadores continuam disponíveis para testes e composição local por arquivos.

O workload remoto recebe da esteira oficial `ProcessamentoRemoto:ContaEsperada`, `Regiao`, `FilaEntradaUrl` e `FilaResultadosOsUrl`. As duas filas devem ser FIFO, distintas e pertencer à conta/região informadas. A identidade é obtida explicitamente da `LabRole` do workload e conferida por STS; não usa credenciais de ambiente ou perfis locais como fallback. A configuração remota deve ser ativada somente na implantação autorizada pela esteira.

O consumidor usa long polling de 20 segundos, uma mensagem por vez e visibilidade de 60 segundos, renovada a cada 20 segundos. Confere os metadados FIFO contra o envelope e exclui a entrada somente após o caso de uso concluir. Falhas de contrato, persistência, conciliação ou renovação preservam a possibilidade de reentrega. Mensagens não reconhecidas não são confirmadas; redrive/DLQ pertencem à infraestrutura da fila.

O publicador trabalha em um loop separado, para que a espera do consumidor não impeça o envio da Outbox. As tentativas preservam `mensagemId` e o agrupamento por OS. O SDK usa timeout de 30 segundos e não repete requisições internamente; Inbox/Outbox e o worker controlam a recuperação. Os testes com SDK substituído e SQL real não comprovam FIFO/DLQ/IAM ou entrega em AWS.

## Validar

A CI em `.github/workflows/ci.yml` reaproveita os jobs da API principal: detecção de escopo, Git Flow/estrutura, build, formatação, testes SQL com Docker, cobertura mínima de 90%, imagem Docker sem push e quality gate obrigatório. A imagem e a identificação da árvore validada ficam em artefato, preservando a referência para o futuro CD. Testes ignorados bloqueiam o aceite.

Neste bootstrap não existem jobs de Kubernetes/Terraform ou CD: os respectivos recursos ainda não foram implementados. Esses jobs e as promoções devem ser derivados das esteiras maduras quando essa frente estiver pronta; não usar um workflow vazio ou um job sempre verde como substituto de deploy. Não há publicação de imagem em registry, operação AWS ou merge automático nesta CI.

```powershell
dotnet restore OficinaMecanica.OrcamentoPagamento.sln
dotnet build OficinaMecanica.OrcamentoPagamento.sln --configuration Release --no-restore
dotnet test OficinaMecanica.OrcamentoPagamento.sln --configuration Release --no-build --settings tests/sonarqube.runsettings --collect:"XPlat Code Coverage"
dotnet format OficinaMecanica.OrcamentoPagamento.sln --verify-no-changes --no-restore
```

Os testes de integração exigem Docker e criam SQL Server descartável com Testcontainers. Validam migrations, persistência real, rollback da transação, concorrência, idempotência, JWT, permissões e restrição por cliente. Não precisam de AWS nem de credenciais reais.

## Limites deste incremento

Os processadores aceitam geração/cancelamento de orçamento e criação de cobrança. A persistência de recebimentos, inclusive cancelamento antecipado, evita repetir efeitos locais. Os testes verificam falha após envio, retomada, rollback, concorrência e ligação por JSON com a Saga em bancos separados.

Os processadores são resolvidos por DI; o publicador recebe um `ITransporteMensagem`. `TransporteArquivos` é um substituto local, derivado da referência do Execução, que escreve em `{diretorio}/{destino}/entrada/{mensagemId}.json`. O worker SQS depende da habilitação explícita descrita acima. Uma falha de processamento exige descartar o escopo de DI antes da nova tentativa; o worker cria um novo escopo em cada operação.

O runtime requer banco/usuário isolados, fila/DLQ e configuração de workload/Gateway fornecidos pelas esteiras correspondentes. O publicador conserva a ordem global das pendências; sua concorrência/política de falha não deve ser ampliada sem testes do transporte real. A instrumentação nativa do Datadog acompanha a implantação. Progresso, dependências e critérios de aceite ficam no backlog canônico da API.

Os testes do provedor substituto comprovam o comportamento local; não comprovam checkout ou Webhook reais do Mercado Pago. As evidências AWS e do fluxo distribuído dependem da integração e da janela de implantação coordenada.
