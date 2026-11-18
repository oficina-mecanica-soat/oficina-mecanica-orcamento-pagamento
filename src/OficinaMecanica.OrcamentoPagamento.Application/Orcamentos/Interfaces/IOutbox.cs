using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;

public interface IOutbox
{
    Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default);
}
