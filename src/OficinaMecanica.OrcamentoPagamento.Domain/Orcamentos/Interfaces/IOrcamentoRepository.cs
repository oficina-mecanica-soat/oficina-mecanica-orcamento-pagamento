using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;

public interface IOrcamentoRepository
{
    Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Orcamento?> ObterPorOrdemEVersaoAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Orcamento orcamento, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Orcamento orcamento, CancellationToken cancellationToken = default);
}
