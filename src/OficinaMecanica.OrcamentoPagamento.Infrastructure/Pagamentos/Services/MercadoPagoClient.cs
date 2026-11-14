using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Services;

public sealed class MercadoPagoClient : IProvedorPagamento
{
    private const decimal ValorMaximoRepresentavel = 9999999999999999.99m;

    public static Uri EnderecoApi { get; } = new("https://api.mercadopago.com/");

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    private readonly HttpClient _httpClient;
    private readonly MercadoPagoOptions _options;

    public MercadoPagoClient(HttpClient httpClient, IOptions<MercadoPagoOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CheckoutResponse> CriarCheckoutAsync(CriarCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidarReferencia(request.Referencia);
        if (string.IsNullOrWhiteSpace(request.Descricao) || request.Descricao.Length > 160 || !ValorValido(request.Valor))
        {
            throw new ArgumentException("Dados do checkout inválidos.", nameof(request));
        }

        var dados = new MercadoPagoPreferenciaRequest(request.Referencia.ToString("D"), _options.UrlNotificacao,
            [new(request.Descricao, 1, request.Valor, "BRL")]);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences")
        {
            Content = JsonContent.Create(dados, options: OpcoesJson)
        };
        // Preferências não documentam a garantia de idempotência da API Payments.
        // O chamador deve persistir a intenção antes daqui e reconciliar qualquer resposta incerta.
        var resposta = await EnviarAsync<MercadoPagoPreferenciaResponse>(requisicao, cancellationToken);
        try
        {
            var checkout = ConverterCheckout(resposta, request.Referencia);
            if (checkout.Valor != request.Valor)
            {
                throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
            }

            return checkout;
        }
        catch (ProvedorPagamentoException)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.ResultadoIncerto);
        }
    }

    public async Task<CheckoutResponse?> ConsultarCheckoutPorReferenciaAsync(Guid referencia, CancellationToken cancellationToken = default)
    {
        ValidarReferencia(referencia);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"checkout/preferences/search?external_reference={referencia:D}&limit=2");
        var pesquisa = await EnviarAsync<MercadoPagoPesquisaPreferenciaResponse>(requisicao, cancellationToken);
        if (pesquisa.Total is null or < 0 || pesquisa.Elements is null)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
        }
        if (pesquisa.Total > 1)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.ReferenciaAmbigua);
        }
        if (pesquisa.Total == 0 && pesquisa.Elements.Length == 0)
        {
            return null;
        }
        if (pesquisa.Total != 1 || pesquisa.Elements.Length != 1 || !IdentificadorPreferenciaValido(pesquisa.Elements[0]?.Id))
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
        }

        // A pesquisa não retorna todos os dados necessários para vincular o checkout com segurança.
        using var consulta = new HttpRequestMessage(HttpMethod.Get, $"checkout/preferences/{Uri.EscapeDataString(pesquisa.Elements[0].Id!)}");
        var resposta = await EnviarAsync<MercadoPagoPreferenciaResponse>(consulta, cancellationToken);
        if (resposta.Id != pesquisa.Elements[0].Id)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
        }

        return ConverterCheckout(resposta, referencia);
    }

    public async Task<PagamentoProvedorResponse> ConsultarPagamentoAsync(string identificadorExterno, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(identificadorExterno, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            throw new ArgumentException("Identificador de pagamento inválido.", nameof(identificadorExterno));
        }

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{id.ToString(CultureInfo.InvariantCulture)}");
        var resposta = await EnviarAsync<MercadoPagoPagamentoResponse>(requisicao, cancellationToken);
        if (resposta.Id != id || resposta.CollectorId != _options.RecebedorId || resposta.LiveMode != false
            || !Guid.TryParseExact(resposta.ExternalReference, "D", out var referencia) || referencia == Guid.Empty
            || resposta.TransactionAmount is null || !ValorValido(resposta.TransactionAmount.Value)
            || resposta.CurrencyId != "BRL" || string.IsNullOrWhiteSpace(resposta.Status))
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
        }

        var status = resposta.Status switch
        {
            "approved" => StatusPagamentoProvedor.Confirmado,
            "authorized" => StatusPagamentoProvedor.Autorizado,
            "pending" or "in_process" or "in_mediation" => StatusPagamentoProvedor.Pendente,
            "rejected" => StatusPagamentoProvedor.Recusado,
            "cancelled" => StatusPagamentoProvedor.Cancelado,
            "refunded" => StatusPagamentoProvedor.Estornado,
            "charged_back" => StatusPagamentoProvedor.Contestado,
            _ => StatusPagamentoProvedor.Desconhecido
        };
        return new(id.ToString(CultureInfo.InvariantCulture), referencia, resposta.TransactionAmount.Value, resposta.CurrencyId, status);
    }

    private CheckoutResponse ConverterCheckout(MercadoPagoPreferenciaResponse resposta, Guid referencia)
    {
        if (!IdentificadorPreferenciaValido(resposta.Id) || resposta.CollectorId != _options.RecebedorId
            || resposta.ExternalReference != referencia.ToString("D") || resposta.Items is not { Length: > 0 }
            || !Uri.TryCreate(resposta.SandboxInitPoint, UriKind.Absolute, out var endereco)
            || endereco.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endereco.UserInfo)
            || endereco.Host is not ("sandbox.mercadopago.com" or "sandbox.mercadopago.com.br"))
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
        }

        decimal total = 0;
        foreach (var item in resposta.Items)
        {
            if (item is null || item.CurrencyId != "BRL" || item.Quantity is null or <= 0 || item.UnitPrice is null
                || !ValorValido(item.UnitPrice.Value) || item.UnitPrice > (ValorMaximoRepresentavel - total) / item.Quantity)
            {
                throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.RespostaInvalida);
            }

            total += item.UnitPrice.Value * item.Quantity.Value;
        }

        return new(resposta.Id!, referencia, total, endereco);
    }

    private async Task<T> EnviarAsync<T>(HttpRequestMessage requisicao, CancellationToken cancellationToken)
    {
        if (!_options.PossuiConfiguracaoValida() || _httpClient.BaseAddress != EnderecoApi)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.ConfiguracaoInvalida);
        }

        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var criacao = requisicao.Method == HttpMethod.Post;
        try
        {
            using var resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                throw new ProvedorPagamentoException(ClassificarFalhaHttp(resposta.StatusCode, criacao));
            }

            return await resposta.Content.ReadFromJsonAsync<T>(OpcoesJson, cancellationToken)
                ?? throw new JsonException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProvedorPagamentoException(criacao ? TipoFalhaProvedorPagamento.ResultadoIncerto : TipoFalhaProvedorPagamento.Indisponivel);
        }
        catch (HttpRequestException)
        {
            throw new ProvedorPagamentoException(criacao ? TipoFalhaProvedorPagamento.ResultadoIncerto : TipoFalhaProvedorPagamento.Indisponivel);
        }
        catch (JsonException)
        {
            throw new ProvedorPagamentoException(criacao ? TipoFalhaProvedorPagamento.ResultadoIncerto : TipoFalhaProvedorPagamento.RespostaInvalida);
        }
    }

    private static bool ValorValido(decimal valor)
    {
        return valor > 0 && valor <= ValorMaximoRepresentavel && decimal.Round(valor, 2) == valor;
    }

    private static TipoFalhaProvedorPagamento ClassificarFalhaHttp(HttpStatusCode status, bool criacao)
    {
        if (status == HttpStatusCode.NotFound)
        {
            return TipoFalhaProvedorPagamento.NaoEncontrado;
        }
        if ((int)status is >= 300 and < 400)
        {
            return criacao ? TipoFalhaProvedorPagamento.ResultadoIncerto : TipoFalhaProvedorPagamento.RespostaInvalida;
        }
        if ((int)status >= 500 || status == HttpStatusCode.TooManyRequests)
        {
            return criacao ? TipoFalhaProvedorPagamento.ResultadoIncerto : TipoFalhaProvedorPagamento.Indisponivel;
        }

        return TipoFalhaProvedorPagamento.RequisicaoRecusada;
    }

    private static bool IdentificadorPreferenciaValido(string? valor)
    {
        return !string.IsNullOrWhiteSpace(valor) && valor.Length <= 128
            && valor.All(caractere => char.IsAsciiLetterOrDigit(caractere) || caractere is '-' or '_');
    }

    private static void ValidarReferencia(Guid referencia)
    {
        if (referencia == Guid.Empty)
        {
            throw new ArgumentException("Referência da cobrança é obrigatória.", nameof(referencia));
        }
    }
}
