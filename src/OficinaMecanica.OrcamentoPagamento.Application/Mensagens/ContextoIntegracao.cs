namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed record ContextoIntegracao(
    Guid SagaId,
    string CorrelationId,
    Guid MensagemId,
    Dictionary<string, string>? TraceContext = null);
