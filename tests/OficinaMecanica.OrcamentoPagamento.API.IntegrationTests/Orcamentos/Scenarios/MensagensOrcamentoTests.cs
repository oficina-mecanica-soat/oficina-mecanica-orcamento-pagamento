using OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Application;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Infrastructure;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class MensagensOrcamentoTests(OrcamentoPagamentoApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private ServiceProvider CriarServicos() => new ServiceCollection().AddLogging().AddApplication()
        .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString
        }).Build()).BuildServiceProvider();
    private async Task ProcessarAsync(MensagemIntegracao mensagem)
    {
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessarMensagemOrcamentoUseCase>().ExecuteAsync(JsonContrato.Serializar(mensagem));
    }
    [Fact]
    public async Task Dado_ReentregasAposReinicio_Quando_Processar_Entao_DevePreservarSnapshotEventoETraceOriginal()
    {
        // Arrange
        var mensagem = MensagemOrcamentoTestDataFactory.Gerar() with
        {
            TraceContext = new()
            {
                ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
            }
        };
        await ProcessarAsync(mensagem);
        await using var inicial = fixture.CriarDbContext();
        var corpo = (await inicial.Outbox.SingleAsync()).Corpo;
        // Act
        await ProcessarAsync(mensagem);
        await ProcessarAsync(mensagem with
        {
            MensagemId = Guid.NewGuid(),
            TraceContext = new()
            {
                ["token"] = "descartar"
            }
        });
        // Assert
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.Orcamentos.CountAsync()).Should().Be(1);
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(2);
        (await verificacao.Outbox.SingleAsync()).Corpo.Should().Be(corpo);
        JsonContrato.LerEnvelope(corpo).TraceContext!["traceparent"].Should().Be(mensagem.TraceContext!["traceparent"]);
        JsonContrato.LerEnvelope(corpo).CausationId.Should().Be(mensagem.MensagemId);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_PayloadDivergente_Quando_ReutilizarIdentidade_Entao_DeveRejeitarSemNovosEfeitos(bool novaMensagem)
    {
        // Arrange
        var mensagem = MensagemOrcamentoTestDataFactory.Gerar();
        await ProcessarAsync(mensagem);
        var dados = JsonContrato.Ler<GerarOrcamentoDados>(mensagem.Dados.GetRawText());
        var divergente = mensagem with
        {
            MensagemId = novaMensagem ? Guid.NewGuid() : mensagem.MensagemId,
            Dados = JsonSerializer.SerializeToElement(dados with
            {
                ClienteId = Guid.NewGuid()
            }, JsonContrato.Options)
        };
        // Act / Assert
        await FluentActions.Awaiting(() => ProcessarAsync(divergente)).Should().ThrowAsync<ContratoMensagemException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Outbox.CountAsync()).Should().Be(1);
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(1);
    }
    [Fact]
    public async Task Dado_CancelamentoAntesDaGeracao_Quando_ReceberGeracaoTardia_Entao_NaoDeveCriarOrcamento()
    {
        // Arrange
        var gerar = MensagemOrcamentoTestDataFactory.Gerar();
        var cancelar = MensagemOrcamentoTestDataFactory.Criar("CancelarOrcamento", new CancelarOrcamentoDados(null, 1, "Cancelamento solicitado"),
            gerar.SagaId, gerar.OrdemServicoId);
        // Act
        await ProcessarAsync(cancelar);
        await ProcessarAsync(gerar);
        await ProcessarAsync(cancelar with
        {
            MensagemId = Guid.NewGuid()
        });
        // Assert
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Orcamentos.CountAsync()).Should().Be(0);
        (await contexto.Set<CancelamentoRecebido>().CountAsync()).Should().Be(1);
        var resultado = JsonContrato.LerEnvelope((await contexto.Outbox.SingleAsync()).Corpo);
        resultado.Tipo.Should().Be("OrcamentoCancelado");
        JsonContrato.Ler<OrcamentoCanceladoDados>(resultado.Dados.GetRawText()).Should().Be(new OrcamentoCanceladoDados(null, 1));
    }
    [Fact]
    public async Task Dado_OrcamentoGerado_Quando_CancelarEReentregar_Entao_DeveManterHistoricoSemDuplicarResultado()
    {
        // Arrange
        var gerar = MensagemOrcamentoTestDataFactory.Gerar();
        await ProcessarAsync(gerar);
        await using var inicial = fixture.CriarDbContext();
        var id = (await inicial.Orcamentos.SingleAsync()).Id;
        var cancelar = MensagemOrcamentoTestDataFactory.Criar("CancelarOrcamento", new CancelarOrcamentoDados(id, 1, "Orçamento recusado"), gerar.SagaId, gerar.OrdemServicoId);
        // Act
        await ProcessarAsync(cancelar);
        await ProcessarAsync(cancelar with
        {
            MensagemId = Guid.NewGuid()
        });
        // Assert
        await using var contexto = fixture.CriarDbContext();
        var orcamento = await contexto.Orcamentos.SingleAsync();
        orcamento.Status.Should().Be(StatusOrcamento.Cancelado);
        orcamento.ValorTotal.Should().Be(240m);
        (await contexto.Outbox.CountAsync()).Should().Be(2);
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(3);
    }
    [Fact]
    public async Task Dado_CancelamentoDeOutraOrdem_Quando_Processar_Entao_DeveRejeitarSemConfirmarRecebimento()
    {
        // Arrange
        var gerar = MensagemOrcamentoTestDataFactory.Gerar();
        await ProcessarAsync(gerar);
        await using var inicial = fixture.CriarDbContext();
        var id = (await inicial.Orcamentos.SingleAsync()).Id;
        var cancelar = MensagemOrcamentoTestDataFactory.Criar("CancelarOrcamento", new CancelarOrcamentoDados(id, null, "Cancelamento"), gerar.SagaId);
        // Act / Assert
        await FluentActions.Awaiting(() => ProcessarAsync(cancelar)).Should().ThrowAsync<ContratoMensagemException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Orcamentos.SingleAsync()).Status.Should().Be(StatusOrcamento.AguardandoAprovacao);
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(1);
    }
    [Fact]
    public async Task Dado_FalhaNaGravacaoDoRecebimento_Quando_Gerar_Entao_DeveReverterOrcamentoEOutbox()
    {
        // Arrange
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var processor = new ProcessarMensagemOrcamentoUseCase(
            new RecebimentoComFalha(sp.GetRequiredService<IRecebimentoMensagemRepository>()),
            sp.GetRequiredService<ICancelamentoMensagemRepository>(), sp.GetRequiredService<Domain.Orcamentos.Interfaces.IOrcamentoRepository>(),
            sp.GetRequiredService<IIntegracaoOrcamentoRepository>(),
            sp.GetRequiredService<Application.Orcamentos.OrcamentoUseCases.GerarOrcamento.GerarOrcamentoUseCase>(),
            sp.GetRequiredService<Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento.CancelarOrcamentoUseCase>(), TimeProvider.System);
        // Act / Assert
        await FluentActions.Awaiting(() => processor.ExecuteAsync(JsonContrato.Serializar(MensagemOrcamentoTestDataFactory.Gerar())))
            .Should().ThrowAsync<IOException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Orcamentos.CountAsync()).Should().Be(0);
        (await contexto.Outbox.CountAsync()).Should().Be(0);
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(0);
    }
    private sealed class RecebimentoComFalha(IRecebimentoMensagemRepository inner) : IRecebimentoMensagemRepository
    {
        public Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken ct) => inner.ExecutarEmTransacaoAsync(operacao, ct);
        public Task<bool> VerificarRepeticaoAsync(Guid id, string chave, string hash, CancellationToken ct) => inner.VerificarRepeticaoAsync(id, chave, hash, ct);
        public Task RegistrarAsync(Guid id, string chave, string hash, CancellationToken ct) => throw new IOException("Falha simulada antes do commit.");
    }
}
