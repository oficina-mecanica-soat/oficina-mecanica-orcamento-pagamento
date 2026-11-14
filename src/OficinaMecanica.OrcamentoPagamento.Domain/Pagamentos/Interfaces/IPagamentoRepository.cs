using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Pagamento?> ObterPorOrcamentoAsync(Guid orcamentoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Pagamento pagamento, CancellationToken cancellationToken = default);
}
