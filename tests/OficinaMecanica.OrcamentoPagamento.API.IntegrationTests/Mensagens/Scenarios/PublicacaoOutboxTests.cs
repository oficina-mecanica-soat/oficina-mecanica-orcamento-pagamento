using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Mensagens.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class PublicacaoOutboxTests(OrcamentoPagamentoApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static MensagemIntegracao Mensagem() => new(Guid.NewGuid(), "OrcamentoGerado", 1, Guid.NewGuid(), Guid.NewGuid(),
        "publicacao-teste", Guid.NewGuid(), DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { orcamentoId = Guid.NewGuid(), versao = 1, valorTotal = 150m }));
    private async Task<MensagemIntegracao> PrepararAsync()
    {
        var mensagem = Mensagem();
        await using var contexto = fixture.CriarDbContext();
        await contexto.AddAsync(RegistroOutbox.Criar(mensagem, 1));
        await contexto.SaveChangesAsync();
        return mensagem;
    }
    [Fact]
    public async Task Dado_FalhaDeTransporte_Quando_Retomar_Entao_DeveAplicarBackoffEPreservarIdentidade()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        var transporte = new TransporteTeste { Falhar = true };
        var relogio = new RelogioTeste();
        await using var contexto = fixture.CriarDbContext();
        var publicador = new PublicadorOutbox(contexto, transporte, relogio);
        // Act
        (await publicador.PublicarProximaAsync()).Should().BeFalse();
        (await publicador.PublicarProximaAsync()).Should().BeFalse();
        relogio.Avancar(3);
        transporte.Falhar = false;
        (await publicador.PublicarProximaAsync()).Should().BeTrue();
        // Assert
        transporte.Corpos.Count.Should().Be(2);
        transporte.Corpos.Distinct().Should().ContainSingle();
        JsonContrato.LerEnvelope(transporte.Corpos.First()).MensagemId.Should().Be(mensagem.MensagemId);
        await using var verificacao = fixture.CriarDbContext();
        var registro = await verificacao.Set<RegistroOutbox>().SingleAsync();
        registro.PublicadoEm.Should().NotBeNull();
        registro.Tentativas.Should().Be(1);
    }
    [Fact]
    public async Task Dado_EnvioConfirmadoComInterrupcaoAntesDaBaixa_Quando_LeaseExpirar_Entao_DeveReenviarMesmoCorpo()
    {
        // Arrange
        var mensagem = await PrepararAsync();
        using var cancelamento = new CancellationTokenSource();
        var transporte = new TransporteTeste { AposEnviar = () => cancelamento.Cancel() };
        var relogio = new RelogioTeste();
        await using (var contexto = fixture.CriarDbContext())
        {
            var publicador = new PublicadorOutbox(contexto, transporte, relogio);
            // Act / Assert
            await FluentActions.Awaiting(() => publicador.PublicarProximaAsync(cancelamento.Token)).Should().ThrowAsync<OperationCanceledException>();
        }
        relogio.Avancar(61);
        transporte.AposEnviar = null;
        await using (var contexto = fixture.CriarDbContext())
        { (await new PublicadorOutbox(contexto, transporte, relogio).PublicarProximaAsync()).Should().BeTrue(); }
        // Assert
        transporte.Corpos.Should().HaveCount(2);
        transporte.Corpos.Distinct().Should().ContainSingle();
        JsonContrato.LerEnvelope(transporte.Corpos.Last()).MensagemId.Should().Be(mensagem.MensagemId);
    }
    [Fact]
    public async Task Dado_DoisPublicadores_Quando_DisputarPendencia_Entao_DeveEnviarSomentePeloDonoDaLease()
    {
        // Arrange
        await PrepararAsync();
        var transporte = new TransporteTeste();
        await using var primeiro = fixture.CriarDbContext();
        await using var segundo = fixture.CriarDbContext();
        // Act
        var resultados = await Task.WhenAll(
            new PublicadorOutbox(primeiro, transporte, TimeProvider.System).PublicarProximaAsync(),
            new PublicadorOutbox(segundo, transporte, TimeProvider.System).PublicarProximaAsync());
        // Assert
        resultados.Count(r => r).Should().Be(1);
        transporte.Corpos.Should().ContainSingle();
    }
    [Fact]
    public async Task Dado_TransporteLocal_Quando_RepetirMensagem_Entao_DevePreservarArquivoERecusarDestinoArbitrario()
    {
        // Arrange
        var diretorio = Path.Combine(Path.GetTempPath(), "oficina-transporte-" + Guid.NewGuid().ToString("N"));
        var transporte = new TransporteArquivos(diretorio);
        var corpo = JsonContrato.Serializar(Mensagem());
        try
        {
            // Act
            await transporte.PublicarAsync("OrdemServico", corpo, default);
            await transporte.PublicarAsync("OrdemServico", corpo, default);
            // Assert
            Directory.GetFiles(diretorio, "*.json", SearchOption.AllDirectories).Should().ContainSingle();
            await FluentActions.Awaiting(() => transporte.PublicarAsync("../destino", corpo, default)).Should().ThrowAsync<ContratoMensagemException>();
        }
        finally { if (Directory.Exists(diretorio)) { Directory.Delete(diretorio, true); } }
    }
    private sealed class RelogioTeste : TimeProvider
    {
        private DateTimeOffset _agora = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(int segundos) => _agora = _agora.AddSeconds(segundos);
    }
    private sealed class TransporteTeste : ITransporteMensagem
    {
        public ConcurrentBag<string> Corpos { get; } = [];
        public bool Falhar { get; set; }
        public Action? AposEnviar { get; set; }
        public async Task PublicarAsync(string destino, string corpo, CancellationToken ct)
        {
            Corpos.Add(corpo);
            if (Falhar) { throw new IOException("Falha simulada no transporte local."); }
            await Task.Delay(30, ct);
            AposEnviar?.Invoke();
        }
    }
}
