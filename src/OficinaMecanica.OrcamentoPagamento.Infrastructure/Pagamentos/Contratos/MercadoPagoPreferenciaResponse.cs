using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;

internal sealed record MercadoPagoPreferenciaResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("external_reference")] string? ExternalReference,
    [property: JsonPropertyName("collector_id")] long? CollectorId,
    [property: JsonPropertyName("sandbox_init_point")] string? SandboxInitPoint,
    [property: JsonPropertyName("items")] MercadoPagoItem[]? Items);
