using System.Text.Json;

namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed record MensagemIntegracao(Guid MensagemId, string Tipo, int Versao, Guid SagaId,
    Guid OrdemServicoId, string CorrelationId, Guid CausationId, DateTimeOffset OcorridoEm, JsonElement Dados);
