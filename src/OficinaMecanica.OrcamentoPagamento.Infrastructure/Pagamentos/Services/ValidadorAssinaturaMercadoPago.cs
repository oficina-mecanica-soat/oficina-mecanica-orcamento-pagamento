using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Services;

public sealed class ValidadorAssinaturaMercadoPago : IValidadorNotificacaoPagamento
{
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<ValidadorAssinaturaMercadoPago> _logger;

    public ValidadorAssinaturaMercadoPago(IOptions<MercadoPagoOptions> options,
        ILogger<ValidadorAssinaturaMercadoPago>? logger = null)
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<ValidadorAssinaturaMercadoPago>.Instance;
    }

    public bool Validar(string? assinatura, string? requisicaoId, string? pagamentoId)
    {
        if (!_options.PossuiConfiguracaoValida() || string.IsNullOrWhiteSpace(assinatura) || assinatura.Length > 256
            || string.IsNullOrWhiteSpace(requisicaoId) || requisicaoId.Length > 128
            || !requisicaoId.All(caractere => char.IsAsciiLetterOrDigit(caractere) || caractere == '-')
            || string.IsNullOrEmpty(pagamentoId) || pagamentoId.Length > 19 || !pagamentoId.All(char.IsAsciiDigit))
        {
            return Recusar("entrada_invalida");
        }

        string? timestamp = null;
        string? hash = null;
        foreach (var parte in assinatura.Split(','))
        {
            var campo = parte.Trim().Split('=', 2);
            if (campo.Length != 2)
            {
                return Recusar("campo_malformado");
            }
            if (campo[0] == "ts" && timestamp is null)
            {
                timestamp = campo[1];
            }
            else if (campo[0] == "v1" && hash is null)
            {
                hash = campo[1];
            }
            else
            {
                return Recusar("campo_duplicado_ou_desconhecido");
            }
        }

        if (timestamp is null || timestamp.Length is < 10 or > 13 || !timestamp.All(char.IsAsciiDigit)
            || hash is null || hash.Length != 64 || !hash.All(char.IsAsciiHexDigit))
        {
            return Recusar("timestamp_ou_hash_invalido");
        }

        var manifesto = $"id:{pagamentoId};request-id:{requisicaoId};ts:{timestamp};";
        var calculado = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.WebhookSecret), Encoding.UTF8.GetBytes(manifesto));
        if (!CryptographicOperations.FixedTimeEquals(calculado, Convert.FromHexString(hash)))
        {
            return Recusar("hash_divergente");
        }
        return true;
    }

    private bool Recusar(string motivo)
    {
        _logger.LogWarning("Assinatura de notificação recusada. Motivo: {MotivoValidacao}.", motivo);
        return false;
    }
}
