using System.Net;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

public sealed class AdaptadoresSqsTests
{
    private const string FilaEntrada = "https://sqs.us-east-1.amazonaws.com/123456789012/entrada-teste.fifo";
    private const string FilaOs = "https://sqs.us-east-1.amazonaws.com/123456789012/resultados-teste.fifo";

    private static MensagemIntegracao CriarMensagem()
    {
        return new MensagemIntegracao(Guid.NewGuid(), "OrcamentoGerado", 1, Guid.NewGuid(), Guid.NewGuid(),
            "correlacao-teste", Guid.NewGuid(), DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { versao = 1 }));
    }

    [Fact]
    public async Task Dado_ReenvioDaOutbox_Quando_Publicar_Entao_DevePreservarIdentidadeGrupoEPayload()
    {
        // Arrange
        var client = new Mock<IAmazonSQS>();
        var enviados = new List<SendMessageRequest>();
        client.Setup(sqs => sqs.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => enviados.Add(request))
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK, MessageId = "confirmado" });
        var transporte = new TransporteSqs(client.Object, FilaOs);
        var mensagem = CriarMensagem();
        var corpo = JsonContrato.Serializar(mensagem);

        // Act
        await transporte.PublicarAsync("OrdemServico", corpo, default);
        await transporte.PublicarAsync("OrdemServico", corpo, default);

        // Assert
        enviados.Should().HaveCount(2);
        enviados.Should().OnlyContain(request => request.QueueUrl == FilaOs
            && request.MessageBody == corpo && request.MessageGroupId == mensagem.OrdemServicoId.ToString("D")
            && request.MessageDeduplicationId == mensagem.MensagemId.ToString("D"));
    }

    [Theory]
    [InlineData(200, "")]
    [InlineData(500, "sem-confirmacao")]
    public async Task Dado_PublicacaoSemConfirmacao_Quando_Enviar_Entao_DevePermitirNovaTentativa(int status, string id)
    {
        // Arrange
        var client = new Mock<IAmazonSQS>();
        client.Setup(sqs => sqs.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = (HttpStatusCode)status, MessageId = id });
        var transporte = new TransporteSqs(client.Object, FilaOs);

        // Act
        var tentativa = () => transporte.PublicarAsync("OrdemServico", JsonContrato.Serializar(CriarMensagem()), default);

        // Assert
        await tentativa.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Dado_DestinoDesconhecido_Quando_Publicar_Entao_DeveRejeitarAntesDoSdk()
    {
        // Arrange
        var client = new Mock<IAmazonSQS>();
        var transporte = new TransporteSqs(client.Object, FilaOs);

        // Act
        var tentativa = () => transporte.PublicarAsync("desconhecido", JsonContrato.Serializar(CriarMensagem()), default);

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://sqs.us-east-1.amazonaws.com/123456789012/fila.fifo")]
    [InlineData("https://sqs.us-east-1.amazonaws.com/123456789012/fila-standard")]
    public void Dado_FilaInvalida_Quando_Compor_Entao_DeveRejeitarSemChamadaRemota(string fila)
    {
        // Arrange
        var client = new Mock<IAmazonSQS>();
        var processador = new Mock<IProcessadorMensagem>();

        // Act
        Action publicar = () => _ = new TransporteSqs(client.Object, fila);
        Action consumir = () => _ = new ConsumidorSqs(client.Object, processador.Object, fila, TimeProvider.System);

        // Assert
        publicar.Should().Throw<ArgumentException>();
        consumir.Should().Throw<ArgumentException>();
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    public async Task Dado_ConsultaSemMensagem_Quando_Consumir_Entao_DeveDistinguirFilaVaziaDeFalha(int status)
    {
        // Arrange
        var cenario = new Cenario();
        cenario.Client.Setup(sqs => sqs.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { HttpStatusCode = (HttpStatusCode)status });

        // Act / Assert
        if (status == 200)
        {
            (await cenario.Consumidor.ProcessarProximaAsync()).Should().BeFalse();
        }
        else
        {
            var tentativa = () => cenario.Consumidor.ProcessarProximaAsync();
            await tentativa.Should().ThrowAsync<InvalidOperationException>();
        }
        cenario.Processador.VerifyNoOtherCalls();
        cenario.VerificarSemConfirmacao();
    }

    [Fact]
    public async Task Dado_CommitPendente_Quando_Consumir_Entao_DeveExcluirSomenteAposConcluir()
    {
        // Arrange
        var cenario = new Cenario();
        var commit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cenario.Processador.Setup(processador => processador.ProcessarAsync(cenario.Corpo, It.IsAny<CancellationToken>())).Returns(commit.Task);

        // Act
        var consumo = cenario.Consumidor.ProcessarProximaAsync();
        cenario.VerificarSemConfirmacao();
        commit.SetResult();

        // Assert
        (await consumo.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        cenario.Client.Verify(sqs => sqs.DeleteMessageAsync(It.Is<DeleteMessageRequest>(request =>
            request.QueueUrl == FilaEntrada && request.ReceiptHandle == "recibo-atual"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("MessageGroupId")]
    [InlineData("MessageDeduplicationId")]
    [InlineData("recibo")]
    [InlineData("json")]
    public async Task Dado_MetadadoOuCorpoInvalido_Quando_Consumir_Entao_DevePreservarEntrada(string campo)
    {
        // Arrange
        var cenario = new Cenario();
        if (campo == "recibo")
        {
            cenario.Recebida.ReceiptHandle = string.Empty;
        }
        else if (campo == "json")
        {
            cenario.Recebida.Body = "{";
        }
        else
        {
            cenario.Recebida.Attributes[campo] = Guid.NewGuid().ToString("D");
        }

        // Act
        var tentativa = () => cenario.Consumidor.ProcessarProximaAsync();

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        cenario.Processador.VerifyNoOtherCalls();
        cenario.VerificarSemConfirmacao();
    }

    [Fact]
    public async Task Dado_FalhaDoCasoDeUso_Quando_Consumir_Entao_NaoDeveConfirmarRecebimento()
    {
        // Arrange
        var cenario = new Cenario();
        cenario.Processador.Setup(processador => processador.ProcessarAsync(cenario.Corpo, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Falha local simulada"));

        // Act
        var tentativa = () => cenario.Consumidor.ProcessarProximaAsync();

        // Assert
        await tentativa.Should().ThrowAsync<IOException>();
        cenario.VerificarSemConfirmacao();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_AckIncerto_Quando_Excluir_Entao_DevePropagarFalhaParaReentrega(bool falharTransporte)
    {
        // Arrange
        var cenario = new Cenario();
        var exclusao = cenario.Client.Setup(sqs => sqs.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()));
        if (falharTransporte)
        {
            exclusao.ThrowsAsync(new IOException("Resposta perdida"));
        }
        else
        {
            exclusao.ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.InternalServerError });
        }

        // Act
        var tentativa = () => cenario.Consumidor.ProcessarProximaAsync();

        // Assert
        await tentativa.Should().ThrowAsync<Exception>();
        cenario.Processador.Verify(processador => processador.ProcessarAsync(cenario.Corpo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_ProcessamentoLongo_Quando_RenovarVisibilidade_Entao_DeveAguardarCommit()
    {
        // Arrange
        var cenario = new Cenario();
        var commit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renovacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cenario.Processador.Setup(processador => processador.ProcessarAsync(cenario.Corpo, It.IsAny<CancellationToken>())).Returns(commit.Task);
        cenario.Client.Setup(sqs => sqs.ChangeMessageVisibilityAsync(It.IsAny<ChangeMessageVisibilityRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => renovacao.TrySetResult())
            .ReturnsAsync(new ChangeMessageVisibilityResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var consumo = cenario.Consumidor.ProcessarProximaAsync();
        cenario.Relogio.Advance(TimeSpan.FromSeconds(20));
        await renovacao.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cenario.VerificarSemConfirmacao();
        commit.SetResult();

        // Assert
        (await consumo.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        cenario.Client.Verify(sqs => sqs.ChangeMessageVisibilityAsync(It.Is<ChangeMessageVisibilityRequest>(request =>
            request.ReceiptHandle == "recibo-atual" && request.VisibilityTimeout == 60), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_FalhaNaRenovacao_Quando_Processar_Entao_DeveCancelarSemConfirmar(bool falharTransporte)
    {
        // Arrange
        var cenario = new Cenario();
        cenario.Processador.Setup(processador => processador.ProcessarAsync(cenario.Corpo, It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct));
        var renovacao = cenario.Client.Setup(sqs => sqs.ChangeMessageVisibilityAsync(It.IsAny<ChangeMessageVisibilityRequest>(), It.IsAny<CancellationToken>()));
        if (falharTransporte)
        {
            renovacao.ThrowsAsync(new IOException("Visibilidade incerta"));
        }
        else
        {
            renovacao.ReturnsAsync(new ChangeMessageVisibilityResponse { HttpStatusCode = HttpStatusCode.InternalServerError });
        }

        // Act
        var consumo = cenario.Consumidor.ProcessarProximaAsync();
        cenario.Relogio.Advance(TimeSpan.FromSeconds(20));
        var tentativa = () => consumo.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await tentativa.Should().ThrowAsync<Exception>().Where(erro => erro.GetType() != typeof(TimeoutException));
        cenario.VerificarSemConfirmacao();
    }

    [Fact]
    public async Task Dado_EncerramentoDoHost_Quando_Cancelar_Entao_DevePreservarEntrada()
    {
        // Arrange
        var cenario = new Cenario();
        using var cancelamento = new CancellationTokenSource();
        cenario.Processador.Setup(processador => processador.ProcessarAsync(cenario.Corpo, It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct));

        // Act
        var consumo = cenario.Consumidor.ProcessarProximaAsync(cancelamento.Token);
        cancelamento.Cancel();
        var tentativa = () => consumo.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await tentativa.Should().ThrowAsync<OperationCanceledException>();
        cenario.VerificarSemConfirmacao();
    }

    private sealed class Cenario
    {
        public Mock<IAmazonSQS> Client { get; } = new();
        public Mock<IProcessadorMensagem> Processador { get; } = new();
        public FakeTimeProvider Relogio { get; } = new();
        public MensagemIntegracao Envelope { get; } = CriarMensagem();
        public string Corpo => JsonContrato.Serializar(Envelope);
        public Message Recebida { get; }
        public ConsumidorSqs Consumidor { get; }

        public Cenario()
        {
            Recebida = new Message
            {
                Body = Corpo,
                ReceiptHandle = "recibo-atual",
                Attributes = new Dictionary<string, string>
                {
                    ["MessageGroupId"] = Envelope.OrdemServicoId.ToString("D"),
                    ["MessageDeduplicationId"] = Envelope.MensagemId.ToString("D")
                }
            };
            Client.Setup(sqs => sqs.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReceiveMessageResponse { HttpStatusCode = HttpStatusCode.OK, Messages = [Recebida] });
            Client.Setup(sqs => sqs.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });
            Processador.Setup(processador => processador.ProcessarAsync(Corpo, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Consumidor = new ConsumidorSqs(Client.Object, Processador.Object, FilaEntrada, Relogio);
        }

        public void VerificarSemConfirmacao()
        {
            Client.Verify(sqs => sqs.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
