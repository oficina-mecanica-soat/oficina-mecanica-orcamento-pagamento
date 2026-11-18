using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Application;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Hosting;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class ProcessamentoRemotoWorkerTests : IAsyncLifetime
{
    private const string FilaEntrada = "https://sqs.us-east-1.amazonaws.com/123456789012/orcamento.fifo";
    private const string FilaOs = "https://sqs.us-east-1.amazonaws.com/123456789012/os.fifo";
    private readonly OrcamentoPagamentoApiFixture _fixture;

    public ProcessamentoRemotoWorkerTests(OrcamentoPagamentoApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CriarServicos(IAmazonSQS sqs)
    {
        return new ServiceCollection().AddLogging().AddApplication()
            .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString
            }).Build())
            .AddProcessamentoRemoto(new(FilaEntrada, FilaOs), sqs).BuildServiceProvider();
    }

    [Fact]
    public async Task Dado_ComandoSqs_Quando_Processar_Entao_DeveConfirmarInboxEOutboxAntesDoAckEEnvio()
    {
        // Arrange
        var mensagem = MensagemOrcamentoTestDataFactory.Gerar();
        var sqs = new Mock<IAmazonSQS>();
        var confirmacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consultas = 0;
        sqs.Setup(client => client.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (ReceiveMessageRequest _, CancellationToken ct) =>
            {
                if (Interlocked.Increment(ref consultas) == 1)
                {
                    return new ReceiveMessageResponse
                    {
                        HttpStatusCode = HttpStatusCode.OK,
                        Messages = [new Message
                        {
                            Body = JsonContrato.Serializar(mensagem), ReceiptHandle = "recibo-do-comando",
                            Attributes = new Dictionary<string, string>
                            {
                                ["MessageGroupId"] = mensagem.OrdemServicoId.ToString("D"),
                                ["MessageDeduplicationId"] = mensagem.MensagemId.ToString("D")
                            }
                        }]
                    };
                }
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new InvalidOperationException("O long polling de teste deve terminar por cancelamento.");
            });
        sqs.Setup(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (DeleteMessageRequest request, CancellationToken ct) =>
            {
                await using var verificacao = _fixture.CriarDbContext();
                (await verificacao.Orcamentos.CountAsync(ct)).Should().Be(1);
                (await verificacao.Set<RecebimentoMensagem>().CountAsync(ct)).Should().Be(1);
                (await verificacao.Outbox.CountAsync(ct)).Should().Be(1);
                request.ReceiptHandle.Should().Be("recibo-do-comando");
                confirmacao.TrySetResult();
                return new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK };
            });
        sqs.Setup(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (SendMessageRequest request, CancellationToken ct) =>
            {
                await using var verificacao = _fixture.CriarDbContext();
                var pendente = await verificacao.Outbox.SingleAsync(ct);
                request.QueueUrl.Should().Be(FilaOs);
                request.MessageBody.Should().Be(pendente.Corpo);
                pendente.PublicadoEm.Should().BeNull();
                publicacao.TrySetResult();
                return new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK, MessageId = "envio-confirmado" };
            });
        await using var services = CriarServicos(sqs.Object);
        var worker = services.GetServices<IHostedService>().OfType<ProcessamentoRemotoWorker>().Single();

        // Act
        await worker.StartAsync(default);
        try
        {
            await confirmacao.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await publicacao.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await AguardarPublicacaoPersistidaAsync();
        }
        finally
        {
            using var encerramento = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(encerramento.Token);
        }

        // Assert
        sqs.Verify(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        sqs.Verify(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_ConsumoIndisponivelEEnvioIncerto_Quando_Retomar_Entao_DevePublicarSemPerderOutbox()
    {
        // Arrange
        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(client => client.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Falha simulada do consumo"));
        sqs.SetupSequence(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Resposta do envio perdida"))
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK, MessageId = "envio-confirmado" });
        await using var services = CriarServicos(sqs.Object);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Application.Orcamentos.Interfaces.IProcessadorMensagem>()
                .ProcessarAsync(JsonContrato.Serializar(MensagemOrcamentoTestDataFactory.Gerar()));
        }
        var worker = services.GetServices<IHostedService>().OfType<ProcessamentoRemotoWorker>().Single();

        // Act
        await worker.StartAsync(default);
        try
        {
            await AguardarPublicacaoPersistidaAsync();
        }
        finally
        {
            using var encerramento = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(encerramento.Token);
        }

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        var registro = await dbContext.Outbox.SingleAsync();
        registro.Tentativas.Should().Be(1);
        registro.PublicadoEm.Should().NotBeNull();
        sqs.Verify(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        sqs.Verify(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task AguardarPublicacaoPersistidaAsync()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var dbContext = _fixture.CriarDbContext();
            if (await dbContext.Outbox.AnyAsync(registro => registro.PublicadoEm != null, limite.Token))
            {
                return;
            }
            await Task.Delay(20, limite.Token);
        }
    }
}
