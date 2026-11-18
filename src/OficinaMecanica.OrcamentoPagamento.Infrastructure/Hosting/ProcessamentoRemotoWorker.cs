using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Hosting;

public sealed class ProcessamentoRemotoWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessamentoRemotoWorker> _logger;
    private readonly TimeProvider _relogio;

    public ProcessamentoRemotoWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProcessamentoRemotoWorker> logger,
        TimeProvider relogio)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _relogio = relogio;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var encerramento = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var consumo = ExecutarLoopAsync(true, encerramento.Token);
        var publicacao = ExecutarLoopAsync(false, encerramento.Token);
        await Task.WhenAny(consumo, publicacao);
        encerramento.Cancel();
        await Task.WhenAll(consumo, publicacao);
    }

    private async Task ExecutarLoopAsync(bool consumir, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processou = false;
                try
                {
                    processou = consumir
                        ? await scope.ServiceProvider.GetRequiredService<ConsumidorSqs>().ProcessarProximaAsync(cancellationToken)
                        : await scope.ServiceProvider.GetRequiredService<PublicadorOutbox>().PublicarProximaAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    // Não registrar envelopes, recibos SQS, credenciais ou detalhes devolvidos pelo SDK.
                    _logger.LogWarning("Processamento SQS pendente de nova tentativa. Etapa: {Etapa}.", consumir ? "Consumo" : "Publicação");
                }
                if (!processou)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), _relogio, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Encerramento solicitado pelo host.
        }
    }
}
