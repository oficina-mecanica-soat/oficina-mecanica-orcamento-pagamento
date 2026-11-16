using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;

internal sealed record MercadoPagoUsuarioResponse(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("tags")] string[]? Tags);
