using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;

internal sealed record MercadoPagoPreferenciaRequest(
    [property: JsonPropertyName("external_reference")] string ExternalReference,
    [property: JsonPropertyName("notification_url")] string NotificationUrl,
    [property: JsonPropertyName("items")] MercadoPagoItem[] Items);
