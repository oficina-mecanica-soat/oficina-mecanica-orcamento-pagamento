namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public bool Ativo { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public long RecebedorId { get; init; }
    public string UrlNotificacao { get; init; } = string.Empty;

    public bool PossuiConfiguracaoValida()
    {
        return Ativo && !string.IsNullOrWhiteSpace(AccessToken)
            && AccessToken.All(caractere => char.IsAsciiLetterOrDigit(caractere) || caractere is '-' or '_' or '.' or '~' or '+' or '/' or '=')
            && !string.IsNullOrWhiteSpace(WebhookSecret) && RecebedorId > 0
            && Uri.TryCreate(UrlNotificacao, UriKind.Absolute, out var endereco)
            && endereco.Scheme == Uri.UriSchemeHttps && !endereco.IsLoopback
            && string.IsNullOrEmpty(endereco.UserInfo) && string.IsNullOrEmpty(endereco.Fragment);
    }
}
