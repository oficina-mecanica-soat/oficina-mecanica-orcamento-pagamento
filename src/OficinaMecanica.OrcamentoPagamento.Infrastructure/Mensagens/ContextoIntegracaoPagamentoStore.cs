using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class ContextoIntegracaoPagamentoStore : IContextoIntegracaoPagamentoStore
{
    private readonly OrcamentoPagamentoDbContext _dbContext;

    public ContextoIntegracaoPagamentoStore(OrcamentoPagamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RegistrarContextoAsync(Guid pagamentoId, ContextoIntegracao contexto, CancellationToken cancellationToken = default)
    {
        var pagamento = await _dbContext.Pagamentos.FindAsync([pagamentoId], cancellationToken)
            ?? throw new InvalidOperationException("Pagamento não foi associado à transação.");
        var entry = _dbContext.Entry(pagamento);
        entry.Property<Guid>("SagaId").CurrentValue = contexto.SagaId;
        entry.Property<Guid>("MensagemOrigemId").CurrentValue = contexto.MensagemId;
        entry.Property<string>("CorrelationId").CurrentValue = contexto.CorrelationId;
        entry.Property<string?>("TraceContext").CurrentValue = contexto.TraceContext is null ? null : JsonContrato.Serializar(ContextoTrace.Filtrar(contexto.TraceContext));
    }

    public async Task<ContextoIntegracao> ObterContextoAsync(Guid pagamentoId, CancellationToken cancellationToken = default)
    {
        var contexto = await _dbContext.Pagamentos.Where(o => o.Id == pagamentoId).Select(o => new
        {
            SagaId = EF.Property<Guid>(o, "SagaId"),
            CorrelationId = EF.Property<string>(o, "CorrelationId"),
            MensagemId = EF.Property<Guid>(o, "MensagemOrigemId"),
            TraceContext = EF.Property<string?>(o, "TraceContext")
        }).SingleAsync(cancellationToken);
        return new(contexto.SagaId, contexto.CorrelationId, contexto.MensagemId,
            contexto.TraceContext is null ? null : ContextoTrace.Filtrar(JsonContrato.Ler<Dictionary<string, string>>(contexto.TraceContext)));
    }

}
