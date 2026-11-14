using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;

internal sealed record MercadoPagoItem(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("quantity")] int? Quantity,
    [property: JsonPropertyName("unit_price")] decimal? UnitPrice,
    [property: JsonPropertyName("currency_id")] string? CurrencyId);
