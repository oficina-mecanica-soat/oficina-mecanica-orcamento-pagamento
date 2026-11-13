using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Builders;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Repositories;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class OrcamentoPersistenciaTests : IAsyncLifetime
{
    [Fact]
    public async Task Dado_PecaRepetidaComPrecosDistintos_Quando_PersistirEReentregar_Entao_DeveManterTodasAsLinhas()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        var primeira = request.PecasInsumos.Single();
        request = request with { PecasInsumos = [primeira, primeira with { Quantidade = 1, ValorUnitario = 50m }] };
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTestDataFactory.Criar("Administrador"));
        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/orcamentos", request);
        var repetida = await client.PostAsJsonAsync("/api/v1/orcamentos", request with { PecasInsumos = request.PecasInsumos.Reverse().ToArray() });
        // Assert
        resposta.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        repetida.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        await using var contexto = _fixture.CriarDbContext();
        var orcamento = await contexto.Orcamentos.Include(o => o.PecasInsumos).SingleAsync();
        orcamento.ValorTotal.Should().Be(290m);
        orcamento.PecasInsumos.Should().HaveCount(2);
        orcamento.PecasInsumos.Select(p => p.ValorUnitario).Should().BeEquivalentTo([45m, 50m]);
        (await contexto.Outbox.CountAsync()).Should().Be(1);
    }
    private readonly OrcamentoPagamentoApiFixture _fixture;
    public OrcamentoPersistenciaTests(OrcamentoPagamentoApiFixture fixture) { _fixture = fixture; }
    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dado_FalhaSqlNaOutbox_Quando_ConfirmarTransacao_Entao_NaoDevePersistirOrcamentoParcial()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        await using var contexto = _fixture.CriarDbContext();
        var repository = new OrcamentoRepository(contexto);
        var integracao = new IntegracaoOrcamentoRepository(contexto);
        var unitOfWork = new UnitOfWork(contexto);
        var orcamento = CriarOrcamento(request);

        // Act
        var salvar = () => unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            await repository.AdicionarAsync(orcamento, ct);
            await integracao.RegistrarContextoAsync(orcamento.Id, request.Contexto, ct);
            var invalida = ResultadosOrcamento.Gerado(orcamento, request.Contexto, TimeProvider.System) with { Tipo = new string('X', 65) };
            await integracao.AdicionarResultadoAsync(invalida, 1, ct);
        });

        // Assert
        await salvar.Should().ThrowAsync<DbUpdateException>();
        await using var verificacao = _fixture.CriarDbContext();
        (await verificacao.Orcamentos.CountAsync()).Should().Be(0);
        (await verificacao.Outbox.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dado_DecisaoECancelamentoConcorrentes_Quando_ConfirmarSnapshotAntigo_Entao_DeveRejeitarEstadoEOutbox()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        var id = await GerarPelaApiAsync(request);
        await using var primeiro = _fixture.CriarDbContext();
        await using var segundo = _fixture.CriarDbContext();
        var r1 = new OrcamentoRepository(primeiro);
        var r2 = new OrcamentoRepository(segundo);
        var aprovar = (await r1.ObterPorIdAsync(id))!;
        var cancelar = (await r2.ObterPorIdAsync(id))!;
        var i1 = new IntegracaoOrcamentoRepository(primeiro);
        var i2 = new IntegracaoOrcamentoRepository(segundo);

        // Act
        aprovar.RegistrarDecisao(1, DecisaoOrcamento.Aprovado);
        await new UnitOfWork(primeiro).ExecutarEmTransacaoAsync(async ct =>
        {
            await r1.AtualizarAsync(aprovar, ct);
            await i1.AdicionarResultadoAsync(ResultadosOrcamento.Decidido(aprovar, request.Contexto, TimeProvider.System), 1, ct);
        });
        cancelar.Cancelar(1, "Cancelamento concorrente");
        var salvarSegundo = () => new UnitOfWork(segundo).ExecutarEmTransacaoAsync(async ct =>
        {
            await r2.AtualizarAsync(cancelar, ct);
            await i2.AdicionarResultadoAsync(ResultadosOrcamento.Cancelado(cancelar, request.Contexto, TimeProvider.System), 1, ct);
        });

        // Assert
        await salvarSegundo.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var verificacao = _fixture.CriarDbContext();
        var persistido = (await new OrcamentoRepository(verificacao).ObterPorIdAsync(id))!;
        persistido.Status.Should().Be(StatusOrcamento.Aprovado);
        persistido.Decisao.Should().Be(DecisaoOrcamento.Aprovado);
        persistido.ValorTotal.Should().Be(240m);
        (await verificacao.Outbox.CountAsync()).Should().Be(2);
        (await verificacao.Outbox.AnyAsync(o => o.Tipo == "OrcamentoCancelado")).Should().BeFalse();
    }

    [Fact]
    public async Task Dado_OutboxConfirmada_Quando_RecarregarEmOutroContexto_Entao_DevePreservarMensagemEContexto()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        var id = await GerarPelaApiAsync(request);
        Guid mensagemId;
        string corpo;
        await using (var primeiro = _fixture.CriarDbContext())
        {
            var registro = await primeiro.Outbox.SingleAsync();
            mensagemId = registro.Id;
            corpo = registro.Corpo;
        }

        // Act
        await using var retomada = _fixture.CriarDbContext();
        var contexto = await new IntegracaoOrcamentoRepository(retomada).ObterContextoAsync(id);
        var registroRetomado = await retomada.Outbox.SingleAsync();

        // Assert
        contexto.Should().Be(request.Contexto);
        registroRetomado.Id.Should().Be(mensagemId);
        registroRetomado.Corpo.Should().Be(corpo);
        registroRetomado.PublicadoEm.Should().BeNull();
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_fixture.ConnectionString).InitialCatalog
            .Should().Be("OrcamentoPagamentoTeste");
    }

    [Fact]
    public async Task Dado_MesmaChaveOsEVersao_Quando_InserirConcorrentemente_Entao_DeveImpedirDuplicacaoDeNegocio()
    {
        // Arrange
        var request = OrcamentoRequestBuilder.Novo();
        await GerarPelaApiAsync(request);
        await using var segundo = _fixture.CriarDbContext();
        var duplicado = CriarOrcamento(request);

        // Act
        var salvar = () => new UnitOfWork(segundo).ExecutarEmTransacaoAsync(async ct =>
        {
            await new OrcamentoRepository(segundo).AdicionarAsync(duplicado, ct);
            var integracao = new IntegracaoOrcamentoRepository(segundo);
            await integracao.RegistrarContextoAsync(duplicado.Id, request.Contexto, ct);
            await integracao.AdicionarResultadoAsync(ResultadosOrcamento.Gerado(duplicado, request.Contexto, TimeProvider.System), 1, ct);
        });

        // Assert
        await salvar.Should().ThrowAsync<DbUpdateException>();
        await using var verificacao = _fixture.CriarDbContext();
        (await verificacao.Orcamentos.CountAsync()).Should().Be(1);
        (await verificacao.Outbox.CountAsync()).Should().Be(1);
    }

    private async Task<Guid> GerarPelaApiAsync(GerarOrcamentoRequest request)
    {
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTestDataFactory.Criar("Administrador"));
        var resposta = await client.PostAsJsonAsync("/api/v1/orcamentos", request);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<OrcamentoResponse>())!.Id;
    }

    private static Orcamento CriarOrcamento(GerarOrcamentoRequest r) =>
        Orcamento.Gerar(r.OrdemServicoId, r.ClienteId, r.Versao,
            r.Servicos.Select(s => ServicoOrcamento.Criar(s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor)).ToArray(),
            r.PecasInsumos.Select(p => PecaInsumoOrcamento.Criar(p.PecaInsumoCatalogoId, p.Descricao, p.Quantidade, p.ValorUnitario)).ToArray());
}
