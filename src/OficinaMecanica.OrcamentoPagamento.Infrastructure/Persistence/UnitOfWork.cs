using Microsoft.EntityFrameworkCore;
using System.Data;
using OficinaMecanica.OrcamentoPagamento.Application.Common;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    public UnitOfWork(OrcamentoPagamentoDbContext dbContext) { _dbContext = dbContext; }

    public async Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            // O consumidor confirma recebimento, efeito local e outbox na transação externa.
            await operacao(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await operacao(cancellationToken);
        // Um único SaveChanges confirma orçamento, contexto e outbox na mesma transação.
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
