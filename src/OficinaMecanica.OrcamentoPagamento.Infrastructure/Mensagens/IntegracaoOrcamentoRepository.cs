using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class IntegracaoOrcamentoRepository : IIntegracaoOrcamentoRepository
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    public IntegracaoOrcamentoRepository(OrcamentoPagamentoDbContext dbContext) { _dbContext = dbContext; }

    public async Task RegistrarContextoAsync(Guid orcamentoId, ContextoIntegracao contexto, CancellationToken cancellationToken = default)
    {
        var orcamento = await _dbContext.Orcamentos.FindAsync([orcamentoId], cancellationToken)
            ?? throw new InvalidOperationException("Orçamento não foi associado à transação.");
        var entry = _dbContext.Entry(orcamento);
        entry.Property<Guid>("SagaId").CurrentValue = contexto.SagaId;
        entry.Property<Guid>("MensagemOrigemId").CurrentValue = contexto.MensagemId;
        entry.Property<string>("CorrelationId").CurrentValue = contexto.CorrelationId;
    }

    public Task<ContextoIntegracao> ObterContextoAsync(Guid orcamentoId, CancellationToken cancellationToken = default) =>
        _dbContext.Orcamentos.Where(o => o.Id == orcamentoId)
            .Select(o => new ContextoIntegracao(EF.Property<Guid>(o, "SagaId"), EF.Property<string>(o, "CorrelationId"),
                EF.Property<Guid>(o, "MensagemOrigemId"))).SingleAsync(cancellationToken);

    public async Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default)
    {
        await _dbContext.Outbox.AddAsync(RegistroOutbox.Criar(mensagem, versaoNegocio), cancellationToken);
    }
}
