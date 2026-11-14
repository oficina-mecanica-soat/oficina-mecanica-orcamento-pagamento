using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Services;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Scenarios;

public sealed class MercadoPagoClientTests
{
    [Fact]
    public async Task Dado_CheckoutValido_Quando_Criar_Entao_DeveAdaptarContratoSemDadosDeCartaoOuTokenNoCorpo()
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler(async (requisicao, cancellationToken) =>
        {
            requisicao.Method.Should().Be(HttpMethod.Post);
            requisicao.RequestUri!.AbsoluteUri.Should().Be("https://api.mercadopago.com/checkout/preferences");
            requisicao.Headers.Authorization!.Scheme.Should().Be("Bearer");
            requisicao.Headers.Authorization.Parameter.Should().Be(dados.Configuracao.AccessToken);
            requisicao.Headers.Contains("X-Idempotency-Key").Should().BeFalse();
            var corpo = await requisicao.Content!.ReadAsStringAsync(cancellationToken);
            corpo.Should().NotContain(dados.Configuracao.AccessToken);
            using var documento = JsonDocument.Parse(corpo);
            documento.RootElement.GetProperty("external_reference").GetString().Should().Be(dados.Referencia.ToString("D"));
            documento.RootElement.GetProperty("notification_url").GetString().Should().Be(dados.Configuracao.UrlNotificacao);
            var item = documento.RootElement.GetProperty("items")[0];
            item.GetProperty("unit_price").GetDecimal().Should().Be(240m);
            item.GetProperty("quantity").GetInt32().Should().Be(1);
            item.GetProperty("currency_id").GetString().Should().Be("BRL");
            return RespostaProvedorHandler.Json(dados.Preferencia());
        });

        // Act
        var checkout = await dados.CriarCliente(handler).CriarCheckoutAsync(dados.CriarRequest());

        // Assert
        checkout.Referencia.Should().Be(dados.Referencia);
        checkout.Valor.Should().Be(240m);
        checkout.Endereco.Host.Should().Be("sandbox.mercadopago.com");
        handler.QuantidadeRequisicoes.Should().Be(1);
    }

    [Fact]
    public async Task Dado_ReferenciaPersistida_Quando_Reconciliar_Entao_DeveConsultarDetalheSemRepetirCriacao()
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((requisicao, _) =>
        {
            requisicao.Method.Should().Be(HttpMethod.Get);
            if (requisicao.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
            {
                requisicao.RequestUri.Query.Should().Contain($"external_reference={dados.Referencia:D}");
                return Task.FromResult(RespostaProvedorHandler.Json("{\"total\":1,\"elements\":[{\"id\":\"42-checkout-teste\"}]}"));
            }
            requisicao.RequestUri.AbsolutePath.Should().Be("/checkout/preferences/42-checkout-teste");
            return Task.FromResult(RespostaProvedorHandler.Json(dados.Preferencia()));
        });

        // Act
        var checkout = await dados.CriarCliente(handler).ConsultarCheckoutPorReferenciaAsync(dados.Referencia);

        // Assert
        checkout!.IdentificadorExterno.Should().Be("42-checkout-teste");
        handler.QuantidadeRequisicoes.Should().Be(2);
    }

    [Theory]
    [InlineData("{\"total\":0,\"elements\":[]}", null)]
    [InlineData("{\"total\":2,\"elements\":[]}", TipoFalhaProvedorPagamento.ReferenciaAmbigua)]
    [InlineData("{}", TipoFalhaProvedorPagamento.RespostaInvalida)]
    [InlineData("{\"total\":-1,\"elements\":[]}", TipoFalhaProvedorPagamento.RespostaInvalida)]
    [InlineData("{\"total\":1,\"elements\":[]}", TipoFalhaProvedorPagamento.RespostaInvalida)]
    [InlineData("{\"total\":1,\"elements\":[null]}", TipoFalhaProvedorPagamento.RespostaInvalida)]
    [InlineData("{\"total\":1,\"elements\":[{\"id\":\"../segredo\"}]}", TipoFalhaProvedorPagamento.RespostaInvalida)]
    public async Task Dado_PesquisaSemResultadoSeguro_Quando_Reconciliar_Entao_NaoDeveCriarOuEscolherArbitrariamente(string corpo, TipoFalhaProvedorPagamento? falha)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json(corpo)));
        var consultar = () => dados.CriarCliente(handler).ConsultarCheckoutPorReferenciaAsync(dados.Referencia);

        // Act / Assert
        if (falha.HasValue)
        {
            (await consultar.Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(falha.Value);
        }
        else
        {
            (await consultar()).Should().BeNull();
        }
        handler.QuantidadeRequisicoes.Should().Be(1);
    }

    [Theory]
    [InlineData("approved", StatusPagamentoProvedor.Confirmado)]
    [InlineData("authorized", StatusPagamentoProvedor.Autorizado)]
    [InlineData("pending", StatusPagamentoProvedor.Pendente)]
    [InlineData("in_process", StatusPagamentoProvedor.Pendente)]
    [InlineData("in_mediation", StatusPagamentoProvedor.Pendente)]
    [InlineData("rejected", StatusPagamentoProvedor.Recusado)]
    [InlineData("cancelled", StatusPagamentoProvedor.Cancelado)]
    [InlineData("refunded", StatusPagamentoProvedor.Estornado)]
    [InlineData("charged_back", StatusPagamentoProvedor.Contestado)]
    [InlineData("novo_estado", StatusPagamentoProvedor.Desconhecido)]
    public async Task Dado_StatusDoProvedor_Quando_Consultar_Entao_DeveMapearSemTratarAutorizacaoComoPagamento(string status, StatusPagamentoProvedor esperado)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json(dados.Pagamento(p => p["status"] = status))));

        // Act
        var pagamento = await dados.CriarCliente(handler).ConsultarPagamentoAsync("123456");

        // Assert
        pagamento.Status.Should().Be(esperado);
        pagamento.Referencia.Should().Be(dados.Referencia);
        pagamento.Valor.Should().Be(240m);
    }

    [Theory]
    [InlineData("id", 999)]
    [InlineData("collector_id", 999)]
    [InlineData("live_mode", true)]
    [InlineData("live_mode", null)]
    [InlineData("external_reference", "outra-referencia")]
    [InlineData("external_reference", "00000000-0000-0000-0000-000000000000")]
    [InlineData("transaction_amount", 0)]
    [InlineData("transaction_amount", null)]
    [InlineData("currency_id", "USD")]
    [InlineData("status", "")]
    public async Task Dado_PagamentoIncompativel_Quando_Consultar_Entao_DeveRejeitarSemConfirmar(string campo, object? valor)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json(dados.Pagamento(p => p[campo] = valor))));

        // Act
        var consultar = () => dados.CriarCliente(handler).ConsultarPagamentoAsync("123456");

        // Assert
        (await consultar.Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.RespostaInvalida);
    }

    [Theory]
    [InlineData("sandbox_init_point", "https://www.mercadopago.com.br/checkout")]
    [InlineData("sandbox_init_point", "https://sandbox.mercadopago.com.evil.example/checkout")]
    [InlineData("sandbox_init_point", "http://sandbox.mercadopago.com/checkout")]
    [InlineData("sandbox_init_point", "https://usuario:senha@sandbox.mercadopago.com/checkout")]
    [InlineData("external_reference", "00000000-0000-0000-0000-000000000000")]
    [InlineData("collector_id", 99)]
    [InlineData("items", null)]
    [InlineData("id", "../outro")]
    public async Task Dado_RespostaDeCriacaoIncompativel_Quando_Receber_Entao_DeveExigirReconciliacao(string campo, object? valor)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json(dados.Preferencia(p => p[campo] = valor))));

        // Act
        var criar = () => dados.CriarCliente(handler).CriarCheckoutAsync(dados.CriarRequest());

        // Assert
        (await criar.Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.ResultadoIncerto);
        handler.QuantidadeRequisicoes.Should().Be(1);
    }

    [Theory]
    [InlineData(401, TipoFalhaProvedorPagamento.RequisicaoRecusada, TipoFalhaProvedorPagamento.RequisicaoRecusada)]
    [InlineData(404, TipoFalhaProvedorPagamento.NaoEncontrado, TipoFalhaProvedorPagamento.NaoEncontrado)]
    [InlineData(429, TipoFalhaProvedorPagamento.ResultadoIncerto, TipoFalhaProvedorPagamento.Indisponivel)]
    [InlineData(500, TipoFalhaProvedorPagamento.ResultadoIncerto, TipoFalhaProvedorPagamento.Indisponivel)]
    [InlineData(302, TipoFalhaProvedorPagamento.ResultadoIncerto, TipoFalhaProvedorPagamento.RespostaInvalida)]
    public async Task Dado_FalhaHttp_Quando_Enviar_Entao_NaoDeveRepetirPostNemExporCorpo(int status, TipoFalhaProvedorPagamento erroPost, TipoFalhaProvedorPagamento erroGet)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json("token-confidencial-na-resposta", (HttpStatusCode)status)));
        var cliente = dados.CriarCliente(handler);

        // Act / Assert
        var criar = await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest())).Should().ThrowAsync<ProvedorPagamentoException>();
        criar.Which.Tipo.Should().Be(erroPost);
        criar.Which.ToString().Should().NotContain("token-confidencial-na-resposta").And.NotContain(dados.Configuracao.AccessToken);
        (await FluentActions.Awaiting(() => cliente.ConsultarPagamentoAsync("123456")).Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(erroGet);
        handler.QuantidadeRequisicoes.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_TimeoutOuFalhaDeRede_Quando_Enviar_Entao_DevePreservarIncertezaSemRetry(bool timeout)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromException<HttpResponseMessage>(
            timeout ? new TaskCanceledException() : new HttpRequestException("detalhe-sensivel")));
        var cliente = dados.CriarCliente(handler);

        // Act / Assert
        (await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest())).Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.ResultadoIncerto);
        (await FluentActions.Awaiting(() => cliente.ConsultarPagamentoAsync("123456")).Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.Indisponivel);
        handler.QuantidadeRequisicoes.Should().Be(2);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{invalid")]
    public async Task Dado_JsonInvalido_Quando_Receber_Entao_DeveDistinguirCriacaoIncertaDeConsultaInvalida(string resposta)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json(resposta)));
        var cliente = dados.CriarCliente(handler);

        // Act / Assert
        (await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest())).Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.ResultadoIncerto);
        (await FluentActions.Awaiting(() => cliente.ConsultarPagamentoAsync("123456")).Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.RespostaInvalida);
    }

    [Theory]
    [InlineData("../123")]
    [InlineData("0")]
    [InlineData("9223372036854775808")]
    [InlineData("")]
    public async Task Dado_IdentificadorInvalido_Quando_Consultar_Entao_NaoDeveEnviarHttp(string id)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => throw new InvalidOperationException());

        // Act / Assert
        await FluentActions.Awaiting(() => dados.CriarCliente(handler).ConsultarPagamentoAsync(id)).Should().ThrowAsync<ArgumentException>();
        handler.QuantidadeRequisicoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_IntegracaoDesabilitadaOuEnderecoAlterado_Quando_Chamar_Entao_NaoDeveEnviarCredencial()
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => throw new InvalidOperationException());
        var desabilitado = dados.CriarCliente(handler, new MercadoPagoOptions());
        var enderecoAlterado = new MercadoPagoClient(new HttpClient(handler) { BaseAddress = new Uri("https://outro.example/") }, Options.Create(dados.Configuracao));

        // Act / Assert
        foreach (var cliente in new[] { desabilitado, enderecoAlterado })
        {
            (await FluentActions.Awaiting(() => cliente.ConsultarPagamentoAsync("123456")).Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.ConfiguracaoInvalida);
        }
        handler.QuantidadeRequisicoes.Should().Be(0);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[{\"currency_id\":\"USD\",\"quantity\":1,\"unit_price\":240}]")]
    [InlineData("[{\"currency_id\":\"BRL\",\"quantity\":0,\"unit_price\":240}]")]
    [InlineData("[{\"currency_id\":\"BRL\",\"quantity\":1,\"unit_price\":0.001}]")]
    [InlineData("[{\"currency_id\":\"BRL\",\"quantity\":2147483647,\"unit_price\":9999999999999999.99}]")]
    [InlineData("[{\"currency_id\":\"BRL\",\"quantity\":1,\"unit_price\":241}]")]
    public async Task Dado_ItensIncompativeis_Quando_ReceberCheckout_Entao_NaoDeveVincularValorOuMoedaDivergente(string itens)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var documento = JsonDocument.Parse(itens);
        using var handler = new RespostaProvedorHandler((_, _) => Task.FromResult(RespostaProvedorHandler.Json(
            dados.Preferencia(p => p["items"] = documento.RootElement.Clone()))));

        // Act / Assert
        (await FluentActions.Awaiting(() => dados.CriarCliente(handler).CriarCheckoutAsync(dados.CriarRequest()))
            .Should().ThrowAsync<ProvedorPagamentoException>()).Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.ResultadoIncerto);
    }

    [Fact]
    public async Task Dado_RequisicaoInvalida_Quando_CriarOuPesquisar_Entao_NaoDeveChamarProvedor()
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var handler = new RespostaProvedorHandler((_, _) => throw new InvalidOperationException());
        var cliente = dados.CriarCliente(handler);

        // Act / Assert
        await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest() with { Referencia = Guid.Empty })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest() with { Valor = 0.001m })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest() with { Descricao = "" })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => cliente.CriarCheckoutAsync(dados.CriarRequest() with { Descricao = new string('X', 161) })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => cliente.ConsultarCheckoutPorReferenciaAsync(Guid.Empty)).Should().ThrowAsync<ArgumentException>();
        handler.QuantidadeRequisicoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_CancelamentoDoChamador_Quando_Consultar_Entao_DevePropagarSemFabricarRecusaFinanceira()
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        using var cancelamento = new CancellationTokenSource();
        using var handler = new RespostaProvedorHandler((_, _) =>
        {
            cancelamento.Cancel();
            throw new OperationCanceledException(cancelamento.Token);
        });

        // Act / Assert
        await FluentActions.Awaiting(() => dados.CriarCliente(handler).ConsultarPagamentoAsync("123456", cancelamento.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
