using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;


public interface ICancelamentoMensagemRepository
{
    Task<CancelamentoRecebido?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken);
    Task RegistrarAsync(CancelamentoRecebido cancelamento, CancellationToken cancellationToken);
}
