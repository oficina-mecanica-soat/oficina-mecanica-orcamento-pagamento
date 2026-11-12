using System.Text.Json;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;

namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public static class ResultadosOrcamento
{
    public static MensagemIntegracao Gerado(Orcamento orcamento, ContextoIntegracao contexto, TimeProvider relogio) =>
        Criar("OrcamentoGerado", orcamento, contexto, new { OrcamentoId = orcamento.Id, orcamento.Versao, orcamento.ValorTotal }, relogio);

    public static MensagemIntegracao Decidido(Orcamento orcamento, ContextoIntegracao contexto, TimeProvider relogio) =>
        Criar("OrcamentoDecidido", orcamento, contexto, new
        {
            OrcamentoId = orcamento.Id,
            orcamento.Versao,
            Decisao = orcamento.Decisao?.ToString() ?? throw new InvalidOperationException("Orçamento sem decisão registrada.")
        }, relogio);

    public static MensagemIntegracao Cancelado(Orcamento orcamento, ContextoIntegracao contexto, TimeProvider relogio) =>
        Criar("OrcamentoCancelado", orcamento, contexto, new { OrcamentoId = orcamento.Id, orcamento.Versao }, relogio);

    private static MensagemIntegracao Criar<T>(string tipo, Orcamento orcamento, ContextoIntegracao contexto, T dados, TimeProvider relogio) =>
        new(Guid.CreateVersion7(), tipo, 1, contexto.SagaId, orcamento.OrdemServicoId, contexto.CorrelationId,
            contexto.MensagemId, relogio.GetUtcNow(), JsonSerializer.SerializeToElement(dados, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
