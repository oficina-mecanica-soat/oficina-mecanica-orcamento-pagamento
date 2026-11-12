using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Common;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    public UnitOfWork(OrcamentoPagamentoDbContext dbContext) { _dbContext = dbContext; }

    public async Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await operacao(cancellationToken);
        // Um único SaveChanges confirma orçamento, contexto e outbox na mesma transação.
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
