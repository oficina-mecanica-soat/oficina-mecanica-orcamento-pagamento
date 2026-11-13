using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Builders;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Controllers;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class OrcamentosControllerTests : IAsyncLifetime
{
    private readonly OrcamentoPagamentoApiFixture _fixture;
    private readonly HttpClient _client;
    public OrcamentosControllerTests(OrcamentoPagamentoApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTestDataFactory.Criar("Administrador"));
        await _fixture.ResetarBancoAsync();
    }
    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Dado_SnapshotValido_Quando_GerarConsultarEDecidir_Entao_DevePersistirUmaVezComOutbox()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();

        // Act
        var criado = await _client.PostAsJsonAsync("/api/v1/orcamentos", request);
        var orcamento = (await criado.Content.ReadFromJsonAsync<OrcamentoResponse>())!;
        var repetido = await _client.PostAsJsonAsync("/api/v1/orcamentos", request with
        { Contexto = request.Contexto with { MensagemId = Guid.NewGuid() } });
        var consulta = await _client.GetAsync($"/api/v1/orcamentos/{orcamento.Id}");
        var operacaoId = Guid.NewGuid();
        var decisao = new { versao = 1, decisao = "Aprovado", operacaoId };
        var aprovar = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao", decisao);
        var aprovarNovamente = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao", decisao);

        // Assert
        criado.StatusCode.Should().Be(HttpStatusCode.Created);
        criado.Headers.Location.Should().NotBeNull();
        repetido.StatusCode.Should().Be(HttpStatusCode.Created);
        (await repetido.Content.ReadFromJsonAsync<OrcamentoResponse>())!.Id.Should().Be(orcamento.Id);
        consulta.StatusCode.Should().Be(HttpStatusCode.OK);
        orcamento.ValorTotal.Should().Be(240m);
        orcamento.Servicos.Single().ServicoId.Should().Be(request.Servicos[0].ServicoId);
        orcamento.Servicos.Single().ServicoCatalogoId.Should().Be(request.Servicos[0].ServicoCatalogoId);
        aprovar.StatusCode.Should().Be(HttpStatusCode.Accepted);
        aprovarNovamente.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await using var contexto = _fixture.CriarDbContext();
        (await contexto.Orcamentos.CountAsync()).Should().Be(1);
        var mensagens = await contexto.Outbox.OrderBy(m => m.CriadoEm).ToArrayAsync();
        mensagens.Should().HaveCount(2);
        mensagens.Should().OnlyContain(m => m.PublicadoEm == null);
        using var envelope = JsonDocument.Parse(mensagens[0].Corpo);
        envelope.RootElement.GetProperty("versao").GetInt32().Should().Be(1);
        envelope.RootElement.GetProperty("sagaId").GetGuid().Should().Be(request.Contexto.SagaId);
        envelope.RootElement.GetProperty("causationId").GetGuid().Should().Be(request.Contexto.MensagemId);
        envelope.RootElement.GetProperty("dados").GetProperty("valorTotal").GetDecimal().Should().Be(240m);
    }

    [Fact]
    public async Task Dado_ClienteAutenticado_Quando_AcessarOutroCliente_Entao_DeveOcultarOrcamentoESemMutacao()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        var criado = await _client.PostAsJsonAsync("/api/v1/orcamentos", request);
        var orcamento = (await criado.Content.ReadFromJsonAsync<OrcamentoResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new("Bearer", TokenTestDataFactory.Criar("Cliente", Guid.NewGuid()));

        // Act
        var consulta = await _client.GetAsync($"/api/v1/orcamentos/{orcamento.Id}");
        var decisao = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao",
            new { versao = 1, decisao = "Recusado", operacaoId = Guid.NewGuid() });

        // Assert
        consulta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        decisao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", TokenTestDataFactory.Criar("Cliente", request.ClienteId));
        (await _client.GetAsync($"/api/v1/orcamentos/{orcamento.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao",
            new { versao = 1, decisao = "Recusado", operacaoId = Guid.NewGuid() })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await using var contexto = _fixture.CriarDbContext();
        (await contexto.Outbox.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Dado_OrcamentoRecusado_Quando_CancelarERepetir_Entao_DevePreservarDecisaoESnapshot()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        var criado = await _client.PostAsJsonAsync("/api/v1/orcamentos", request);
        var orcamento = (await criado.Content.ReadFromJsonAsync<OrcamentoResponse>())!;
        await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao",
            new { versao = 1, decisao = "Recusado", operacaoId = Guid.NewGuid() });
        var cancelamento = new { versao = 1, operacaoId = Guid.NewGuid(), motivo = "Recusa do cliente" };

        // Act
        var cancelar = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/cancelar", cancelamento);
        var repetir = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/cancelar", cancelamento);

        // Assert
        cancelar.StatusCode.Should().Be(HttpStatusCode.Accepted);
        repetir.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var resultado = (await cancelar.Content.ReadFromJsonAsync<OrcamentoResponse>())!;
        resultado.Status.Should().Be("Cancelado");
        resultado.Decisao.Should().Be("Recusado");
        resultado.ValorTotal.Should().Be(240m);
        await using var contexto = _fixture.CriarDbContext();
        (await contexto.Outbox.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Dado_MesmaChaveDeNegocio_Quando_AlterarSnapshot_Entao_DeveRetornarConflitoSemSobrescrever()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        await _client.PostAsJsonAsync("/api/v1/orcamentos", request);

        // Act
        var divergente = await _client.PostAsJsonAsync("/api/v1/orcamentos", request with
        { Servicos = [request.Servicos[0] with { Valor = 200m }] });
        var novaVersao = await _client.PostAsJsonAsync("/api/v1/orcamentos", request with { Versao = 2 });

        // Assert
        divergente.StatusCode.Should().Be(HttpStatusCode.Conflict);
        novaVersao.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var contexto = _fixture.CriarDbContext();
        var historico = await contexto.Orcamentos.OrderBy(o => o.Versao).ToArrayAsync();
        historico.Should().HaveCount(2);
        historico.Should().OnlyContain(o => o.ValorTotal == 240m);
        (await contexto.Outbox.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Dado_EntradaInvalida_Quando_Gerar_Entao_DeveRetornarTodosErrosSemPersistir()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo() with { OrdemServicoId = Guid.Empty, ClienteId = Guid.Empty, Versao = 0 };

        // Act
        var resposta = await _client.PostAsJsonAsync("/api/v1/orcamentos", request);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var erro = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        erro.RootElement.GetProperty("tipo").GetString().Should().Be("Validacao");
        erro.RootElement.GetProperty("erros").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
        await using var contexto = _fixture.CriarDbContext();
        (await contexto.Orcamentos.CountAsync()).Should().Be(0);
        (await contexto.Outbox.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("sem-token", HttpStatusCode.Unauthorized)]
    [InlineData("Cliente", HttpStatusCode.Forbidden)]
    [InlineData("Mecanico", HttpStatusCode.Forbidden)]
    public async Task Dado_PerfilSemPermissao_Quando_Gerar_Entao_DeveBloquear(string perfil, HttpStatusCode esperado)
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = perfil == "sem-token" ? null : new("Bearer", TokenTestDataFactory.Criar(perfil));

        // Act
        var resposta = await _client.PostAsJsonAsync("/api/v1/orcamentos", OrcamentoRequestBuilder.Novo());

        // Assert
        resposta.StatusCode.Should().Be(esperado);
    }

    [Fact]
    public async Task Dado_ClienteSemClaim_Quando_ConsultarOuDecidir_Entao_DeveNegar()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new("Bearer", TokenTestDataFactory.Criar("Cliente"));

        // Act
        var consulta = await _client.GetAsync($"/api/v1/orcamentos/{Guid.NewGuid()}");
        var decisao = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{Guid.NewGuid()}/decisao",
            new { versao = 1, decisao = "Aprovado", operacaoId = Guid.NewGuid() });

        // Assert
        consulta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        decisao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dado_DecisaoIncompativel_Quando_Enviar_Entao_DevePreservarVersaoERegras()
    {
        // Arrange
        var criado = await _client.PostAsJsonAsync("/api/v1/orcamentos", OrcamentoRequestBuilder.Novo());
        var orcamento = (await criado.Content.ReadFromJsonAsync<OrcamentoResponse>())!;

        // Act
        var antiga = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao",
            new { versao = 2, decisao = "Aprovado", operacaoId = Guid.NewGuid() });
        var inexistente = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{Guid.NewGuid()}/cancelar",
            new { versao = 1, operacaoId = Guid.NewGuid(), motivo = "Motivo" });
        var invalida = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao",
            new { versao = 0, decisao = "Outro", operacaoId = Guid.Empty });

        // Assert
        antiga.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        invalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dado_ServicoIsolado_Quando_ConsultarHealthESwagger_Entao_DeveDocumentarRotasProtegidas()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var health = await _client.GetAsync("/api/health");
        var swagger = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        swagger.StatusCode.Should().Be(HttpStatusCode.OK);
        using var documento = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        var operacao = documento.RootElement.GetProperty("paths").GetProperty("/api/v1/orcamentos").GetProperty("post");
        operacao.GetProperty("responses").TryGetProperty("401", out _).Should().BeTrue();
        operacao.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("assinatura")]
    [InlineData("emissor")]
    [InlineData("audiencia")]
    [InlineData("expiracao")]
    public async Task Dado_JwtInvalido_Quando_Consultar_Entao_DeveRejeitarAutenticacao(string falha)
    {
        // Arrange
        var token = TokenTestDataFactory.Criar("Administrador",
            issuer: falha == "emissor" ? "outro-emissor" : null,
            audience: falha == "audiencia" ? "outra-audiencia" : null,
            expires: falha == "expiracao" ? DateTime.UtcNow.AddMinutes(-1) : null,
            chave: falha == "assinatura" ? "outra-chave-local-de-teste-com-32-bytes-ou-mais" : null);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var resposta = await _client.GetAsync($"/api/v1/orcamentos/{Guid.NewGuid()}");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dado_OrcamentoExistente_Quando_EnviarDecisaoECancelamentoInvalidos_Entao_DevePreservarOutbox()
    {
        // Arrange
        var criado = await _client.PostAsJsonAsync("/api/v1/orcamentos", OrcamentoRequestBuilder.Novo());
        var orcamento = (await criado.Content.ReadFromJsonAsync<OrcamentoResponse>())!;

        // Act
        var decisao = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/decisao",
            new { versao = 0, decisao = "Aprovado", operacaoId = Guid.Empty });
        var cancelamento = await _client.PostAsJsonAsync($"/api/v1/orcamentos/{orcamento.Id}/cancelar",
            new { versao = 0, operacaoId = Guid.Empty, motivo = "" });

        // Assert
        decisao.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        cancelamento.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var contexto = _fixture.CriarDbContext();
        (await contexto.Outbox.CountAsync()).Should().Be(1);
    }
}
