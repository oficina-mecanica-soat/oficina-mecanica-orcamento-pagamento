using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Repositories;

public sealed class PagamentoRepository : IPagamentoRepository
{
    private readonly OrcamentoPagamentoDbContext _dbContext;

    public PagamentoRepository(OrcamentoPagamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Pagamentos.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<Pagamento?> ObterPorOrcamentoAsync(Guid orcamentoId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Pagamentos.SingleOrDefaultAsync(p => p.OrcamentoId == orcamentoId, cancellationToken);
    }

    public async Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default)
    {
        await _dbContext.Pagamentos.AddAsync(pagamento, cancellationToken);
    }

    public Task AtualizarAsync(Pagamento pagamento, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _dbContext.Pagamentos.Update(pagamento);
        return Task.CompletedTask;
    }
}
