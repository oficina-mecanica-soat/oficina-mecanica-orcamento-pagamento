using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;

internal sealed record MercadoPagoPesquisaPreferenciaResponse(
    [property: JsonPropertyName("total")] int? Total,
    [property: JsonPropertyName("elements")] MercadoPagoPreferenciaResponse[]? Elements);
