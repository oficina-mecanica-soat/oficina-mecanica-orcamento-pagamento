using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record CancelarOrcamentoDados(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OrcamentoId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Versao,
    string Motivo);
