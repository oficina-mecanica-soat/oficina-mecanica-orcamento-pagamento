using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Observabilidade;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

public sealed class ObservabilidadeMensagensTests
{
    private static string CriarCorpo()
    {
        return JsonContrato.Serializar(new MensagemIntegracao(Guid.NewGuid(), "ResultadoTeste", 1,
            Guid.NewGuid(), Guid.NewGuid(), "correlacao-segura", Guid.NewGuid(), DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new { documento = "dado-pessoal-nao-registravel", token = "segredo-nao-registravel" }),
            new Dictionary<string, string> { ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dado_Processamento_Quando_EncerrarEscopo_Entao_RegistrarResultadoSemDadosSensiveis(bool concluido)
    {
        // Arrange
        var logger = new LoggerTeste();
        var observabilidade = new ObservabilidadeMensagens(logger);
        var corpo = CriarCorpo();

        // Act
        using (var escopo = observabilidade.Iniciar("mensagem.processar", corpo))
        {
            if (concluido)
            {
                escopo.Concluir();
            }
        }

        // Assert
        logger.Eventos.Should().ContainSingle();
        logger.Eventos[0].Nivel.Should().Be(concluido ? LogLevel.Information : LogLevel.Warning);
        logger.Eventos[0].Texto.Should().Contain(concluido ? "concluida" : "falha");
        logger.Eventos[0].Texto.Should().NotContain("dado-pessoal-nao-registravel").And.NotContain("segredo-nao-registravel");
        logger.Escopos.Should().ContainSingle();
        logger.Escopos[0].Keys.Should().NotContain("dados").And.NotContain("documento").And.NotContain("token");
        logger.Escopos[0]["x_correlation_id"].Should().Be("correlacao-segura");
        logger.Ativos.Should().Be(0);
    }

    [Fact]
    public void Dado_EscoposAninhados_Quando_Encerrar_Entao_NaoVazarMetadadosNemDescartarDuasVezes()
    {
        // Arrange
        var logger = new LoggerTeste();
        var observabilidade = new ObservabilidadeMensagens(logger);

        // Act
        using (var externo = observabilidade.Iniciar("mensagem.processar", CriarCorpo()))
        {
            var interno = observabilidade.Iniciar("mensagem.publicar", CriarCorpo());
            logger.Ativos.Should().Be(2);
            interno.Concluir();
            interno.Dispose();
            interno.Dispose();
            logger.Ativos.Should().Be(1);
            externo.Concluir();
        }

        // Assert
        logger.Ativos.Should().Be(0);
        logger.Eventos.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("JSON inválido com segredo-nao-registravel")]
    [InlineData("{}")]
    public void Dado_EnvelopeInvalido_Quando_Instrumentar_Entao_NaoSubstituirValidacaoNemRegistrarCorpo(string corpo)
    {
        // Arrange
        var logger = new LoggerTeste();
        var observabilidade = new ObservabilidadeMensagens(logger);

        // Act
        Action instrumentar = () => observabilidade.Iniciar("mensagem.processar", corpo).Dispose();

        // Assert
        instrumentar.Should().NotThrow();
        logger.Eventos.Should().ContainSingle();
        logger.Eventos[0].Texto.Should().NotContain("segredo-nao-registravel");
    }

    [Fact]
    public void Dado_ProvedorDeLogsComFalha_Quando_Instrumentar_Entao_NaoAlterarResultadoDeNegocio()
    {
        // Arrange
        var observabilidade = new ObservabilidadeMensagens(new LoggerTeste { Falhar = true });

        // Act
        Action instrumentar = () =>
        {
            using var escopo = observabilidade.Iniciar("mensagem.publicar", CriarCorpo());
            escopo.Concluir();
        };

        // Assert
        instrumentar.Should().NotThrow();
    }

    [Fact]
    public void Dado_SemTracer_Quando_Capturar_Entao_PreservarSomenteCarrierValidadoSemBaggage()
    {
        // Arrange
        var observabilidade = new ObservabilidadeMensagens(new LoggerTeste());
        var trace = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var origem = new Dictionary<string, string> { ["traceparent"] = trace, ["baggage"] = "segredo" };

        // Act
        var capturado = observabilidade.CapturarContexto(origem);

        // Assert
        capturado.Should().ContainSingle().Which.Key.Should().Be("traceparent");
        capturado!["traceparent"].Should().Be(trace);
        origem.Should().HaveCount(2);
        observabilidade.CapturarContexto(new Dictionary<string, string> { ["traceparent"] = "invalido" }).Should().BeNull();
        observabilidade.CapturarContexto(null).Should().BeNull();
    }

    private sealed class LoggerTeste : ILogger<ObservabilidadeMensagens>
    {
        public List<(LogLevel Nivel, string Texto)> Eventos { get; } = [];
        public List<Dictionary<string, object?>> Escopos { get; } = [];
        public int Ativos { get; private set; }
        public bool Falhar { get; init; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (Falhar)
            {
                throw new InvalidOperationException("Provedor indisponível.");
            }
            Escopos.Add(new Dictionary<string, object?>((IEnumerable<KeyValuePair<string, object?>>)(object)state));
            Ativos++;
            return new EscopoTeste(() => Ativos--);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (Falhar)
            {
                throw new InvalidOperationException("Provedor indisponível.");
            }
            Eventos.Add((logLevel, formatter(state, exception)));
        }

        private sealed class EscopoTeste : IDisposable
        {
            private readonly Action _descartar;

            public EscopoTeste(Action descartar)
            {
                _descartar = descartar;
            }

            public void Dispose() => _descartar();
        }
    }
}
