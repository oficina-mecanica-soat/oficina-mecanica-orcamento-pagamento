using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class PagamentosHttpTests : IAsyncLifetime
{
    private const string SegredoTeste = "segredo-apenas-para-teste-de-notificacao";
    private readonly OrcamentoPagamentoApiFixture _fixture;
    private readonly EstadoProvedorPagamentoTeste _provedor = new();

    public PagamentosHttpTests(OrcamentoPagamentoApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CriarCliente(string? papel = null, Guid? clienteId = null)
    {
        var client = _fixture.CreateClient(services =>
        {
            services.RemoveAll<IProvedorPagamento>();
            services.AddSingleton(_provedor);
            services.AddScoped<IProvedorPagamento, ProvedorPagamentoTeste>();
            services.AddSingleton<IOptions<MercadoPagoOptions>>(Options.Create(new MercadoPagoOptions
            {
                Ativo = true,
                AccessToken = "token-apenas-teste",
                WebhookSecret = SegredoTeste,
                RecebedorId = 123,
                UrlNotificacao = "https://notificacao.test/api/v1/pagamentos/notificacoes/mercado-pago"
            }));
        });
        if (papel is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTestDataFactory.Criar(papel, clienteId));
        }
        return client;
    }

    private async Task<Pagamento> PrepararAsync()
    {
        var orcamento = Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1,
            [ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), "Troca de óleo", 240m)], []);
        orcamento.RegistrarDecisao(1, DecisaoOrcamento.Aprovado);
        var pagamento = Pagamento.Solicitar(orcamento.OrdemServicoId, orcamento.Id, orcamento.ClienteId, 1, 240m);
        pagamento.IniciarCriacao();
        pagamento.RegistrarCheckout("preferencia", new Uri("https://sandbox.mercadopago.com.br/checkout/teste"), 240m);
        await using var dbContext = _fixture.CriarDbContext();
        dbContext.Orcamentos.Add(orcamento);
        dbContext.Pagamentos.Add(pagamento);
        foreach (var entidade in new object[] { orcamento, pagamento })
        {
            dbContext.Entry(entidade).Property("SagaId").CurrentValue = Guid.NewGuid();
            dbContext.Entry(entidade).Property("MensagemOrigemId").CurrentValue = Guid.NewGuid();
            dbContext.Entry(entidade).Property("CorrelationId").CurrentValue = "correlacao-http";
        }
        await dbContext.SaveChangesAsync();
        _provedor.Pagamento = new("123456", pagamento.Id, 240m, "BRL", StatusPagamentoProvedor.Confirmado);
        return pagamento;
    }

    private static HttpRequestMessage Notificar(string tipo = "payment", string idCorpo = "123456", bool assinar = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pagamentos/notificacoes/mercado-pago?data.id=123456")
        {
            Content = JsonContent.Create(new { type = tipo, data = new { id = idCorpo } })
        };
        request.Headers.Add("x-request-id", "requisicao-teste");
        if (assinar)
        {
            var manifesto = "id:123456;request-id:requisicao-teste;ts:1742505638683;";
            var hash = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SegredoTeste), Encoding.UTF8.GetBytes(manifesto)));
            request.Headers.Add("x-signature", $"ts=1742505638683,v1={hash}");
        }
        return request;
    }

    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData("Cliente", false, HttpStatusCode.Forbidden)]
    [InlineData("Cliente", true, HttpStatusCode.OK)]
    [InlineData("Administrador", false, HttpStatusCode.OK)]
    [InlineData("Atendente", false, HttpStatusCode.OK)]
    public async Task Dado_IdentidadeDoUsuario_Quando_ConsultarPagamento_Entao_DeveRespeitarPermissao(string? papel, bool clienteProprietario, HttpStatusCode esperado)
    {
        // Arrange
        var pagamento = await PrepararAsync();
        using var client = CriarCliente(papel, clienteProprietario ? pagamento.ClienteId : null);

        // Act
        using var response = await client.GetAsync($"/api/v1/pagamentos/{pagamento.Id}");

        // Assert
        response.StatusCode.Should().Be(esperado);
        _provedor.ConsultasPagamento.Should().Be(0);
    }

    [Fact]
    public async Task Dado_OutroCliente_Quando_ConsultarPagamento_Entao_DeveOcultarRecurso()
    {
        // Arrange
        var pagamento = await PrepararAsync();
        using var client = CriarCliente("Cliente", Guid.NewGuid());

        // Act
        using var response = await client.GetAsync($"/api/v1/pagamentos/{pagamento.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dado_NotificacaoAssinadaRepetida_Quando_Conferir_Entao_DeveConfirmarUmaVezSemExporDados()
    {
        // Arrange
        await PrepararAsync();
        using var client = CriarCliente();

        // Act
        using var primeira = await client.SendAsync(Notificar());
        using var repetida = await client.SendAsync(Notificar());

        // Assert
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        repetida.StatusCode.Should().Be(HttpStatusCode.OK);
        (await primeira.Content.ReadAsStringAsync()).Should().BeEmpty();
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.Confirmado);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoConfirmado")).Should().Be(1);
        JsonContrato.LerEnvelope((await dbContext.Outbox.SingleAsync()).Corpo).CorrelationId.Should().Be("correlacao-http");
    }

    [Theory]
    [InlineData("payment", "123456", false, HttpStatusCode.Unauthorized)]
    [InlineData("merchant_order", "123456", true, HttpStatusCode.BadRequest)]
    [InlineData("payment", "999999", true, HttpStatusCode.BadRequest)]
    public async Task Dado_NotificacaoInvalida_Quando_Receber_Entao_DeveRejeitarAntesDaConsulta(string tipo, string id, bool assinar, HttpStatusCode esperado)
    {
        // Arrange
        await PrepararAsync();
        using var client = CriarCliente();

        // Act
        using var response = await client.SendAsync(Notificar(tipo, id, assinar));

        // Assert
        response.StatusCode.Should().Be(esperado);
        _provedor.ConsultasPagamento.Should().Be(0);
    }

    [Fact]
    public async Task Dado_ValorDivergenteNoProvedor_Quando_Notificar_Entao_DeveRetornarConflitoSemConfirmar()
    {
        // Arrange
        await PrepararAsync();
        _provedor.Pagamento = _provedor.Pagamento! with { Valor = 239m };
        using var client = CriarCliente();

        // Act
        using var response = await client.SendAsync(Notificar());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.AguardandoPagamento);
        (await dbContext.Outbox.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(StatusPagamentoProvedor.Desconhecido)]
    [InlineData(StatusPagamentoProvedor.Estornado)]
    [InlineData(StatusPagamentoProvedor.Contestado)]
    public async Task Dado_EstadoExigeConciliacao_Quando_Notificar_Entao_NaoDeveConfirmarNemReverterPagamento(StatusPagamentoProvedor status)
    {
        // Arrange
        await PrepararAsync();
        using var client = CriarCliente();
        using var aprovado = await client.SendAsync(Notificar());
        aprovado.StatusCode.Should().Be(HttpStatusCode.OK);
        _provedor.Pagamento = _provedor.Pagamento! with { Status = status };

        // Act
        using var response = await client.SendAsync(Notificar());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.Confirmado);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoConfirmado")).Should().Be(1);
    }

    [Fact]
    public async Task Dado_ProvedorIndisponivel_Quando_Notificar_Entao_DevePermitirReentregaSemConfirmacaoFalsa()
    {
        // Arrange
        await PrepararAsync();
        _provedor.Indisponivel = true;
        using var client = CriarCliente();

        // Act
        using var response = await client.SendAsync(Notificar());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.AguardandoPagamento);
        (await dbContext.Outbox.CountAsync()).Should().Be(0);
    }
}
