using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Repositories;

public sealed class OrcamentoRepository : IOrcamentoRepository
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    public OrcamentoRepository(OrcamentoPagamentoDbContext dbContext) { _dbContext = dbContext; }

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Consultar().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Orcamento?> ObterPorOrdemEVersaoAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken = default) =>
        Consultar().SingleOrDefaultAsync(o => o.OrdemServicoId == ordemServicoId && o.Versao == versao, cancellationToken);

    public async Task AdicionarAsync(Orcamento orcamento, CancellationToken cancellationToken = default)
    {
        await _dbContext.Orcamentos.AddAsync(orcamento, cancellationToken);
    }

    public Task AtualizarAsync(Orcamento orcamento, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _dbContext.Orcamentos.Update(orcamento);
        return Task.CompletedTask;
    }

    private IQueryable<Orcamento> Consultar() => _dbContext.Orcamentos.Include(o => o.Servicos).Include(o => o.PecasInsumos);
}
