using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;

public interface ITransporteMensagem
{
    Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken);
}
