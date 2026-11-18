using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;

internal sealed record MercadoPagoPagamentoResponse(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("external_reference")] string? ExternalReference,
    [property: JsonPropertyName("collector_id")] long? CollectorId,
    [property: JsonPropertyName("transaction_amount")] decimal? TransactionAmount,
    [property: JsonPropertyName("currency_id")] string? CurrencyId,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("live_mode")] bool? LiveMode);
