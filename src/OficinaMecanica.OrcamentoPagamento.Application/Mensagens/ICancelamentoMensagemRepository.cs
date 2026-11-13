namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed record CancelamentoRecebido(Guid OrdemServicoId, int Versao, Guid SagaId, string Motivo);
public interface ICancelamentoMensagemRepository
{
    Task<CancelamentoRecebido?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken);
    Task RegistrarAsync(CancelamentoRecebido cancelamento, CancellationToken cancellationToken);
}
