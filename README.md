# Orçamento/Pagamento — Oficina Mecânica

Serviço responsável por orçamentos versionados e, na evolução prevista, cobrança. Este primeiro incremento funciona **localmente**: gera, consulta, aprova, recusa e cancela orçamentos em SQL Server próprio. Não houve publicação no GitHub nem implantação AWS.

## Arquitetura e referências

As decisões continuam centralizadas na [API principal](https://github.com/oficina-mecanica-soat/oficina-mecanica-api): ADR-0024, ADR-0025, ADR-0026, RFC-0007 e `docs/projeto/backlog-tecnico.md` (F4-07/F4-08). Consulte também o [AGENTS.md](AGENTS.md).

O serviço preserva a organização da API principal: `Domain`, `Application`, `Infrastructure` e `API`; funcionalidades agrupadas por contexto; Request/Response/Validator/UseCase; `Result<T>` e erros tipados; AutoMapper; FluentValidation; DI explícita; autenticação JWT; tratamento centralizado de exceções e logs estruturados. As configurações de JWT, Swagger, correlação, respostas HTTP e observabilidade foram adaptadas das implementações existentes. Os testes seguem Factories, AAA, xUnit e FluentAssertions.

Não existem referências de projeto, consultas SQL, entidades compartilhadas ou migrations da API principal. Os identificadores externos são referências de negócio, sem chaves estrangeiras entre bancos.

## Comportamento implementado

- Cada orçamento pertence a uma OS, um cliente e uma versão. Serviços e peças mantêm identificadores, descrições, quantidades e preços congelados; uma nova versão não altera a anterior.
- A geração repetida com o mesmo snapshot e Saga retorna o orçamento existente. Conteúdo ou Saga divergentes para a mesma OS/versão retornam conflito.
- Aprovação e recusa só ocorrem enquanto o orçamento aguarda aprovação. Repetir a mesma decisão não produz outro resultado. Cancelar preserva o snapshot e eventual decisão anterior; repetir o cancelamento exige o mesmo motivo.
- Orçamento, contexto de integração e resultado na outbox são gravados na mesma transação SQL. `rowversion` protege alterações concorrentes; índices únicos impedem versões e resultados duplicados.
- Resultados `OrcamentoGerado`, `OrcamentoDecidido` e `OrcamentoCancelado` ficam persistidos no envelope v1 do RFC-0007, com Saga, correlação, causação e traceparent validado. O publicador local mantém corpo/ID após falhas e usa lease e backoff. **Ainda não há adaptador ou consumo SQS ativo.**
- O processador de comandos confirma recebimento, efeito e outbox em uma transação SQL. Reentregas compatíveis não duplicam efeitos; mesma identidade com conteúdo divergente é rejeitada. Cancelamento anterior à geração deixa um registro persistente que impede reabrir aquela versão, inclusive pelo caso de uso HTTP.

O contexto da Saga é metadado de persistência/integração, sem dependência tecnológica no Domain. O total usa `decimal(18,2)`; valores inválidos, duplicatas e totais que excedam esse limite são rejeitados antes da persistência.

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

O serviço permanece em andamento. O núcleo de mensagens já processa geração/cancelamento e grava recebimentos, inclusive cancelamento antecipado; a ligação local com a Saga foi validada por JSON e bancos separados. Os testes também verificam falha após envio, retomada e publicadores concorrentes.

Os processadores são resolvidos por DI; o publicador recebe um `ITransporteMensagem`. `TransporteArquivos` é um substituto local, derivado da referência do Execução, que escreve em `{diretorio}/{destino}/entrada/{mensagemId}.json`. Não existe worker automático habilitado no host, nem configuração que acione AWS neste incremento. Uma falha de processamento exige descartar o escopo de DI antes da nova tentativa.

Estão pendentes: adaptador/worker SQS e validação de FIFO/DLQ/visibilidade/IAM; ativação do fluxo nas rotas de OS e corte do ownership; CI/CD e governança do novo repositório derivados das esteiras existentes; SQL/usuário isolados no RDS, fila/DLQ e integração Kubernetes/Gateway. O publicador local conserva a ordem global das pendências e só pode ter sua concorrência/política de falha ampliada com os testes do transporte real. A instrumentação nativa do Datadog precisa acompanhar o futuro deploy.

F4-08 ainda requer checkout Mercado Pago em teste, notificação verificada, consulta ao provedor e confirmação financeira idempotente. Nenhum endpoint deste incremento confirma pagamento ou autoriza entrega de OS. As evidências AWS e do fluxo distribuído dependem da integração e da janela de implantação coordenada.
