using System.Text.Json;
using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed record MensagemIntegracao(Guid MensagemId, string Tipo, int Versao, Guid SagaId,
    Guid OrdemServicoId, string CorrelationId, Guid CausationId, DateTimeOffset OcorridoEm, JsonElement Dados,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string>? TraceContext = null);
