using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Observabilidade;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class ConsumidorSqs
{
    private readonly ObservabilidadeMensagens? _observabilidade;
    private readonly IAmazonSQS _client;
    private readonly IProcessadorMensagem _processador;
    private readonly string _filaEntradaUrl;
    private readonly TimeProvider _relogio;

    public ConsumidorSqs(IAmazonSQS client, IProcessadorMensagem processador, string filaEntradaUrl, TimeProvider relogio, ObservabilidadeMensagens? observabilidade = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(processador);
        ArgumentNullException.ThrowIfNull(relogio);
        ContratoFilaSqs.ValidarUrl(filaEntradaUrl);
        _observabilidade = observabilidade;
        _client = client;
        _processador = processador;
        _filaEntradaUrl = filaEntradaUrl;
        _relogio = relogio;
    }

    public async Task<bool> ProcessarProximaAsync(CancellationToken cancellationToken = default)
    {
        var resposta = await _client.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _filaEntradaUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 20,
            VisibilityTimeout = 60,
            MessageSystemAttributeNames = ["MessageGroupId", "MessageDeduplicationId"]
        }, cancellationToken);
        if (resposta.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("Consulta SQS sem confirmação válida; não presumir fila vazia.");
        }
        var recebida = resposta.Messages?.SingleOrDefault();
        if (recebida is null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(recebida.ReceiptHandle))
        {
            throw new ContratoMensagemException("RECIBO_SQS_AUSENTE");
        }

        using var processamentoCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var processamento = ProcessarAsync(recebida, processamentoCts.Token);
        var renovacao = RenovarVisibilidadeAsync(recebida.ReceiptHandle, processamento, processamentoCts.Token);
        try
        {
            if (await Task.WhenAny(processamento, renovacao) == renovacao)
            {
                await renovacao;
            }
            // O processador só conclui após confirmar a transação de estado/deduplicação/outbox.
            await processamento;
            processamentoCts.Cancel();
            await renovacao;
            cancellationToken.ThrowIfCancellationRequested();
            var confirmacao = await _client.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = _filaEntradaUrl,
                ReceiptHandle = recebida.ReceiptHandle
            }, cancellationToken);
            if (confirmacao.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException("Exclusão SQS sem confirmação válida; preservar a possibilidade de reentrega.");
            }
            return true;
        }
        finally
        {
            processamentoCts.Cancel();
            // Observa e encerra ambas as tarefas sem esconder a falha original nem confirmar a entrada.
            try
            {
                await processamento;
            }
            catch (Exception)
            {
                // A falha original é propagada pelo bloco principal.
            }
            try
            {
                await renovacao;
            }
            catch (Exception)
            {
                // Observar a tarefa encerrada sem substituir a falha original.
            }
        }
    }

    private async Task ProcessarAsync(Message recebida, CancellationToken ct)
    {
        var envelope = JsonContrato.LerEnvelope(recebida.Body);
        if (!Guid.TryParse(recebida.Attributes?.GetValueOrDefault("MessageGroupId"), out var grupo) || grupo != envelope.OrdemServicoId
            || !Guid.TryParse(recebida.Attributes?.GetValueOrDefault("MessageDeduplicationId"), out var deduplicacao) || deduplicacao != envelope.MensagemId)
        {
            throw new ContratoMensagemException("METADADOS_FIFO_DIVERGENTES");
        }
        using var escopo = _observabilidade?.Iniciar("orcamento_pagamento.processar", recebida.Body);
        await _processador.ProcessarAsync(recebida.Body, ct);
        escopo?.Concluir();
    }

    private async Task RenovarVisibilidadeAsync(string recibo, Task processamento, CancellationToken ct)
    {
        try
        {
            while (!processamento.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), _relogio, ct);
                if (processamento.IsCompleted)
                {
                    return;
                }
                var resposta = await _client.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                {
                    QueueUrl = _filaEntradaUrl,
                    ReceiptHandle = recibo,
                    VisibilityTimeout = 60
                }, ct);
                if (resposta.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException("Renovação SQS sem confirmação válida; o processamento deve ser interrompido.");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Encerramento normal após conclusão ou cancelamento do processamento.
        }
    }
}
