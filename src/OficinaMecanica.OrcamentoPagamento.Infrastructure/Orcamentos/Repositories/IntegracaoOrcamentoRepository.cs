using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Repositories;

public sealed class IntegracaoOrcamentoRepository : IIntegracaoOrcamentoRepository
{
    private readonly OrcamentoPagamentoDbContext _dbContext;

    public IntegracaoOrcamentoRepository(OrcamentoPagamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RegistrarContextoAsync(Guid orcamentoId, ContextoIntegracao contexto, CancellationToken cancellationToken = default)
    {
        var orcamento = await _dbContext.Orcamentos.FindAsync([orcamentoId], cancellationToken)
            ?? throw new InvalidOperationException("Orçamento não foi associado à transação.");
        var entry = _dbContext.Entry(orcamento);
        entry.Property<Guid>("SagaId").CurrentValue = contexto.SagaId;
        entry.Property<Guid>("MensagemOrigemId").CurrentValue = contexto.MensagemId;
        entry.Property<string>("CorrelationId").CurrentValue = contexto.CorrelationId;
        entry.Property<string?>("TraceContext").CurrentValue = contexto.TraceContext is null ? null : JsonContrato.Serializar(ContextoTrace.Filtrar(contexto.TraceContext));
    }

    public async Task<ContextoIntegracao> ObterContextoAsync(Guid orcamentoId, CancellationToken cancellationToken = default)
    {
        var contexto = await _dbContext.Orcamentos.Where(o => o.Id == orcamentoId).Select(o => new
        {
            SagaId = EF.Property<Guid>(o, "SagaId"),
            CorrelationId = EF.Property<string>(o, "CorrelationId"),
            MensagemId = EF.Property<Guid>(o, "MensagemOrigemId"),
            TraceContext = EF.Property<string?>(o, "TraceContext")
        }).SingleAsync(cancellationToken);
        return new(contexto.SagaId, contexto.CorrelationId, contexto.MensagemId,
            contexto.TraceContext is null ? null : ContextoTrace.Filtrar(JsonContrato.Ler<Dictionary<string, string>>(contexto.TraceContext)));
    }

    public async Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default)
    {
        await _dbContext.Outbox.AddAsync(RegistroOutbox.Criar(mensagem, versaoNegocio), cancellationToken);
    }
}
