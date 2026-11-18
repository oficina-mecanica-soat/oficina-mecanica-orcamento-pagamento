using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class TransporteSqs : ITransporteMensagem
{
    private readonly IAmazonSQS _client;
    private readonly string _filaResultadosOsUrl;

    public TransporteSqs(IAmazonSQS client, string filaResultadosOsUrl)
    {
        ArgumentNullException.ThrowIfNull(client);
        ContratoFilaSqs.ValidarUrl(filaResultadosOsUrl);
        _client = client;
        _filaResultadosOsUrl = filaResultadosOsUrl;
    }

    public async Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken)
    {
        if (destino != "OrdemServico")
        {
            throw new ContratoMensagemException("DESTINO_RESULTADO_INVALIDO");
        }
        var mensagem = JsonContrato.LerEnvelope(corpo);
        var resposta = await _client.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _filaResultadosOsUrl,
            MessageBody = JsonContrato.Serializar(mensagem),
            MessageGroupId = mensagem.OrdemServicoId.ToString("D"),
            MessageDeduplicationId = mensagem.MensagemId.ToString("D")
        }, cancellationToken);
        if (resposta.HttpStatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(resposta.MessageId))
        {
            throw new InvalidOperationException("Publicação SQS sem confirmação válida; a Outbox deve repetir o envio.");
        }
    }
}
