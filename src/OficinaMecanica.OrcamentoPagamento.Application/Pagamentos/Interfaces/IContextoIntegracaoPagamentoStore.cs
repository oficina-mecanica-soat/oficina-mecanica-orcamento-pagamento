using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;

public interface IContextoIntegracaoPagamentoStore
{
    Task RegistrarContextoAsync(Guid pagamentoId, ContextoIntegracao contexto, CancellationToken cancellationToken = default);
    Task<ContextoIntegracao> ObterContextoAsync(Guid pagamentoId, CancellationToken cancellationToken = default);
}
