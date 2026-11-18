using System.Text.Json;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Services;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;

internal sealed class MercadoPagoTestDataFactory
{
    public Guid Referencia { get; } = Guid.NewGuid();
    public MercadoPagoOptions Configuracao { get; } = new()
    {
        Ativo = true,
        AccessToken = "token-apenas-para-testes",
        WebhookSecret = "chave-apenas-para-testes",
        RecebedorId = 42,
        UrlNotificacao = "https://oficina.example.com/notificacoes/mercado-pago"
    };

    public CriarCheckoutRequest CriarRequest()
    {
        return new(Referencia, "Pagamento da ordem de serviço", 240m);
    }

    public MercadoPagoClient CriarCliente(HttpMessageHandler handler, MercadoPagoOptions? configuracao = null)
    {
        return new(new HttpClient(handler) { BaseAddress = MercadoPagoClient.EnderecoApi }, Options.Create(configuracao ?? Configuracao));
    }

    public string Preferencia(Action<Dictionary<string, object?>>? alterar = null)
    {
        var resposta = new Dictionary<string, object?>
        {
            ["id"] = "42-checkout-teste",
            ["external_reference"] = Referencia.ToString("D"),
            ["collector_id"] = 42,
            ["sandbox_init_point"] = "https://sandbox.mercadopago.com/checkout/v1/redirect?pref_id=42-checkout-teste",
            ["init_point"] = "https://www.mercadopago.com.br/checkout/v1/redirect?pref_id=42-checkout-teste",
            ["items"] = new[] { new { title = "Ordem de serviço", quantity = 1, unit_price = 240m, currency_id = "BRL" } }
        };
        alterar?.Invoke(resposta);
        return JsonSerializer.Serialize(resposta);
    }

    public string Pagamento(Action<Dictionary<string, object?>>? alterar = null)
    {
        var resposta = new Dictionary<string, object?>
        {
            ["id"] = 123456,
            ["external_reference"] = Referencia.ToString("D"),
            ["collector_id"] = 42,
            ["transaction_amount"] = 240m,
            ["currency_id"] = "BRL",
            ["status"] = "approved",
            ["live_mode"] = false
        };
        alterar?.Invoke(resposta);
        return JsonSerializer.Serialize(resposta);
    }
}
