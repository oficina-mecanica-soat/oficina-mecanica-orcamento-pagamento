using System.Text.Json;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;

namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public static class ResultadosPagamento
{
    public static MensagemIntegracao Criar(string tipo, Pagamento pagamento, ContextoIntegracao contexto, TimeProvider relogio)
    {
        var dados = new ResultadoPagamentoDados(pagamento.Id, pagamento.OrcamentoId, pagamento.Versao, pagamento.Valor);
        return new MensagemIntegracao(Guid.CreateVersion7(), tipo, 1, contexto.SagaId, pagamento.OrdemServicoId,
            contexto.CorrelationId, contexto.MensagemId, relogio.GetUtcNow(),
            JsonSerializer.SerializeToElement(dados, JsonContrato.Options), ContextoTrace.Filtrar(contexto.TraceContext));
    }
}
