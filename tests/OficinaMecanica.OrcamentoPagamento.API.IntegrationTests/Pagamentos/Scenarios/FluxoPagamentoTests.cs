using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Application;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ConferirPagamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.DetalharPagamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ProcessarMensagemPagamento;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Infrastructure;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class FluxoPagamentoTests : IAsyncLifetime
{
    private readonly OrcamentoPagamentoApiFixture _fixture;
    private readonly EstadoProvedorPagamentoTeste _provedor = new();

    public FluxoPagamentoTests(OrcamentoPagamentoApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CriarServicos()
    {
        var services = new ServiceCollection().AddLogging().AddApplication()
            .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString
            }).Build());
        services.RemoveAll<IProvedorPagamento>();
        services.AddSingleton(_provedor);
        services.AddScoped<IProvedorPagamento, ProvedorPagamentoTeste>();
        return services.BuildServiceProvider();
    }

    private async Task<MensagemIntegracao> PrepararAsync(bool aprovar = true)
    {
        var gerar = MensagemOrcamentoTestDataFactory.Gerar();
        await using var services = CriarServicos();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessarMensagemOrcamentoUseCase>().ExecuteAsync(JsonContrato.Serializar(gerar));
        var dbContext = scope.ServiceProvider.GetRequiredService<OrcamentoPagamentoDbContext>();
        var orcamento = await dbContext.Orcamentos.SingleAsync();
        if (aprovar)
        {
            var decisao = await scope.ServiceProvider.GetRequiredService<RegistrarDecisaoOrcamentoUseCase>()
                .ExecuteAsync(new(orcamento.Id, 1, DecisaoOrcamento.Aprovado, Guid.NewGuid(), null));
            decisao.Sucesso.Should().BeTrue();
        }
        return MensagemOrcamentoTestDataFactory.Criar("CriarCobranca", new CriarCobrancaDados(orcamento.Id, 1), gerar.SagaId, gerar.OrdemServicoId);
    }

    private async Task ProcessarAsync(MensagemIntegracao mensagem)
    {
        await using var services = CriarServicos();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessarMensagemPagamentoUseCase>().ExecuteAsync(JsonContrato.Serializar(mensagem));
    }

    private async Task ConferirAsync(StatusPagamentoProvedor status, string identificador = "123", decimal valor = 240m, Guid? referencia = null)
    {
        await using var dbContext = _fixture.CriarDbContext();
        var pagamento = await dbContext.Pagamentos.SingleAsync();
        _provedor.Pagamento = new(identificador, referencia ?? pagamento.Id, valor, "BRL", status);
        await using var services = CriarServicos();
        await using var scope = services.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<ConferirPagamentoUseCase>().ExecuteAsync(new(identificador, Guid.NewGuid()));
        resultado.Sucesso.Should().BeTrue();
    }

    [Fact]
    public async Task Dado_ComandoReentregue_Quando_CriarCobranca_Entao_DevePersistirUmaIntencaoCheckoutEEvento()
    {
        // Arrange
        var mensagem = await PrepararAsync();

        // Act
        await ProcessarAsync(mensagem);
        await ProcessarAsync(mensagem);
        await ProcessarAsync(mensagem with { MensagemId = Guid.NewGuid() });

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        var pagamento = await dbContext.Pagamentos.SingleAsync();
        pagamento.Status.Should().Be(StatusPagamento.AguardandoPagamento);
        pagamento.Valor.Should().Be(240m);
        _provedor.Criacoes.Should().Be(1);
        var evento = await dbContext.Outbox.SingleAsync(o => o.Tipo == "CobrancaCriada");
        JsonContrato.LerEnvelope(evento.Corpo).CausationId.Should().Be(mensagem.MensagemId);
    }

    [Fact]
    public async Task Dado_RespostaPerdidaAposCriacao_Quando_Reentregar_Entao_DeveConciliarSemOutroPost()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        _provedor.FalharAposCriacao = true;
        var primeira = () => ProcessarAsync(mensagem);
        await primeira.Should().ThrowAsync<ProvedorPagamentoException>();

        // Act
        await ProcessarAsync(mensagem);

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.AguardandoPagamento);
        _provedor.Criacoes.Should().Be(1);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "CobrancaCriada")).Should().Be(1);
    }

    [Fact]
    public async Task Dado_CriacaoIncertaSemResultadoNaConsulta_Quando_Reentregar_Entao_DeveManterPendenteSemNovoPost()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        _provedor.FalharAposCriacao = true;
        var primeira = () => ProcessarAsync(mensagem);
        await primeira.Should().ThrowAsync<ProvedorPagamentoException>();
        _provedor.Checkout = null;

        // Act
        var repeticao = () => ProcessarAsync(mensagem);

        // Assert
        await repeticao.Should().ThrowAsync<InvalidOperationException>();
        _provedor.Criacoes.Should().Be(1);
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.CriacaoEmAndamento);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "CobrancaCriada")).Should().Be(0);
    }

    [Fact]
    public async Task Dado_ProvedorNaoConfigurado_Quando_RetomarAposConfigurar_Entao_DeveCriarUmaVez()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        _provedor.Indisponivel = true;
        var tentativa = () => ProcessarAsync(mensagem);
        await tentativa.Should().ThrowAsync<ProvedorPagamentoException>();
        await using (var dbContext = _fixture.CriarDbContext())
        {
            (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.Solicitado);
        }

        // Act
        _provedor.Indisponivel = false;
        await ProcessarAsync(mensagem);

        // Assert
        _provedor.Criacoes.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_OrcamentoNaoAprovadoOuContextoDivergente_Quando_Cobrar_Entao_DeveRejeitarSemEfeitos(bool contextoDivergente)
    {
        // Arrange
        var mensagem = await PrepararAsync(contextoDivergente);
        if (contextoDivergente)
        {
            mensagem = mensagem with { SagaId = Guid.NewGuid() };
        }

        // Act
        var tentativa = () => ProcessarAsync(mensagem);

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.CountAsync()).Should().Be(0);
        _provedor.Criacoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_PagamentoConfirmado_Quando_RepetirOuReceberEventoAtrasado_Entao_DeveManterConfirmacaoUnica()
    {
        // Arrange
        await ProcessarAsync(await PrepararAsync());

        // Act
        await ConferirAsync(StatusPagamentoProvedor.Confirmado);
        await ConferirAsync(StatusPagamentoProvedor.Confirmado);
        await ConferirAsync(StatusPagamentoProvedor.Pendente);
        await ConferirAsync(StatusPagamentoProvedor.Recusado, "124");

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        var pagamento = await dbContext.Pagamentos.SingleAsync();
        pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        pagamento.IdentificadorPagamentoProvedor.Should().Be("123");
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoConfirmado")).Should().Be(1);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoRecusado")).Should().Be(0);
        await using var services = CriarServicos();
        var resposta = await services.GetRequiredService<DetalharPagamentoUseCase>().ExecuteAsync(new(pagamento.Id, pagamento.ClienteId));
        resposta.Valor!.EnderecoCheckout.Should().BeNull();
        (await services.GetRequiredService<DetalharPagamentoUseCase>().ExecuteAsync(new(pagamento.Id, Guid.NewGuid()))).Sucesso.Should().BeFalse();
    }

    [Theory]
    [InlineData(StatusPagamentoProvedor.Pendente)]
    [InlineData(StatusPagamentoProvedor.Autorizado)]
    [InlineData(StatusPagamentoProvedor.Cancelado)]
    public async Task Dado_PagamentoNaoAprovado_Quando_Conferir_Entao_NaoDeveConfirmar(StatusPagamentoProvedor status)
    {
        // Arrange
        await ProcessarAsync(await PrepararAsync());

        // Act
        await ConferirAsync(status);

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.AguardandoPagamento);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoConfirmado")).Should().Be(0);
    }

    [Fact]
    public async Task Dado_TentativaRecusada_Quando_OutraForAprovada_Entao_DeveRegistrarResultadosSemDuplicar()
    {
        // Arrange
        await ProcessarAsync(await PrepararAsync());
        await ConferirAsync(StatusPagamentoProvedor.Recusado);
        await ConferirAsync(StatusPagamentoProvedor.Recusado);

        // Act
        await ConferirAsync(StatusPagamentoProvedor.Confirmado, "124");

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.Confirmado);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoRecusado")).Should().Be(1);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "PagamentoConfirmado")).Should().Be(1);
    }

    [Fact]
    public async Task Dado_ValorDivergente_Quando_Conferir_Entao_DeveRejeitarSemReciboOuConfirmacao()
    {
        // Arrange
        await ProcessarAsync(await PrepararAsync());
        await using var inicial = _fixture.CriarDbContext();
        var recibos = await inicial.Set<RecebimentoMensagem>().CountAsync();

        // Act
        var tentativa = () => ConferirAsync(StatusPagamentoProvedor.Confirmado, valor: 239m);

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        await using var verificacao = _fixture.CriarDbContext();
        (await verificacao.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.AguardandoPagamento);
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(recibos);
        (await verificacao.Outbox.CountAsync(o => o.Tipo == "PagamentoConfirmado")).Should().Be(0);
    }

    [Fact]
    public async Task Dado_FalhaNaOutbox_Quando_Confirmar_Entao_DeveReverterPagamentoERecibo()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        await ProcessarAsync(mensagem);
        int recibos;
        await using (var dbContext = _fixture.CriarDbContext())
        {
            var pagamento = await dbContext.Pagamentos.SingleAsync();
            var contexto = new ContextoIntegracao(mensagem.SagaId, mensagem.CorrelationId, Guid.NewGuid());
            dbContext.Outbox.Add(RegistroOutbox.Criar(ResultadosPagamento.Criar("PagamentoConfirmado", pagamento, contexto, TimeProvider.System), pagamento.Versao));
            await dbContext.SaveChangesAsync();
            recibos = await dbContext.Set<RecebimentoMensagem>().CountAsync();
        }

        // Act
        var tentativa = () => ConferirAsync(StatusPagamentoProvedor.Confirmado);

        // Assert
        await tentativa.Should().ThrowAsync<DbUpdateException>();
        await using var verificacao = _fixture.CriarDbContext();
        (await verificacao.Pagamentos.SingleAsync()).Status.Should().Be(StatusPagamento.AguardandoPagamento);
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(recibos);
    }

    [Fact]
    public async Task Dado_DuasEntregasConcorrentes_Quando_DisputarCriacao_Entao_DevePermitirUmUnicoPost()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        _provedor.Indisponivel = true;
        var tentativaInicial = () => ProcessarAsync(mensagem);
        await tentativaInicial.Should().ThrowAsync<ProvedorPagamentoException>();
        _provedor.Indisponivel = false;
        var consultas = 0;
        var barreira = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _provedor.AntesConsultarCheckout = async ct =>
        {
            if (Interlocked.Increment(ref consultas) == 2)
            {
                barreira.TrySetResult();
            }
            await barreira.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        };

        // Act
        var resultados = await Task.WhenAll(
            CapturarFalhaAsync(() => ProcessarAsync(mensagem)),
            CapturarFalhaAsync(() => ProcessarAsync(mensagem)));

        // Assert
        resultados.Count(erro => erro is null).Should().Be(1);
        resultados.Single(erro => erro is not null).Should().BeOfType<DbUpdateConcurrencyException>();
        _provedor.Criacoes.Should().Be(1);
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Pagamentos.CountAsync()).Should().Be(1);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "CobrancaCriada")).Should().Be(1);
    }

    private static async Task<Exception?> CapturarFalhaAsync(Func<Task> operacao)
    {
        try
        {
            await operacao();
            return null;
        }
        catch (Exception erro)
        {
            return erro;
        }
    }

    [Fact]
    public async Task Dado_CobrancaSolicitada_Quando_CancelarOrcamento_Entao_DeveImpedirCompensacao()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        await ProcessarAsync(mensagem);
        await using var services = CriarServicos();
        var orcamentoId = JsonContrato.Ler<CriarCobrancaDados>(mensagem.Dados.GetRawText()).OrcamentoId;

        // Act
        var resultado = await services.GetRequiredService<CancelarOrcamentoUseCase>().ExecuteAsync(new(orcamentoId, 1, Guid.NewGuid(), "Cancelamento tardio"));

        // Assert
        resultado.Erro!.Tipo.Should().Be(Application.Common.TipoErro.Conflito);
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Orcamentos.SingleAsync()).Status.Should().Be(StatusOrcamento.Aprovado);
        (await dbContext.Outbox.CountAsync(o => o.Tipo == "OrcamentoCancelado")).Should().Be(0);
    }
}
