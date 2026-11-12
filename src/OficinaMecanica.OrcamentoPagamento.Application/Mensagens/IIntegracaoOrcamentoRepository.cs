namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public interface IIntegracaoOrcamentoRepository
{
    Task RegistrarContextoAsync(Guid orcamentoId, ContextoIntegracao contexto, CancellationToken cancellationToken = default);
    Task<ContextoIntegracao> ObterContextoAsync(Guid orcamentoId, CancellationToken cancellationToken = default);
    Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default);
}
