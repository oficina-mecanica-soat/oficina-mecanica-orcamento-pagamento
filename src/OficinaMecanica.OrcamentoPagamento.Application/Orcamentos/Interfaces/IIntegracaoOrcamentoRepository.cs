using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;

public interface IIntegracaoOrcamentoRepository
{
    Task RegistrarContextoAsync(Guid orcamentoId, ContextoIntegracao contexto, CancellationToken cancellationToken = default);
    Task<ContextoIntegracao> ObterContextoAsync(Guid orcamentoId, CancellationToken cancellationToken = default);
    Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default);
}
