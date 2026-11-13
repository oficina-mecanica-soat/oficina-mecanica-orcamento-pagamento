namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public interface ITransporteMensagem
{
    Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken);
}
