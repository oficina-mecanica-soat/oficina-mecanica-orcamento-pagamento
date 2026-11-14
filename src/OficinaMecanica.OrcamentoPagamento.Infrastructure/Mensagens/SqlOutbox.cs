using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Observabilidade;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class SqlOutbox : IOutbox
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    private readonly ObservabilidadeMensagens? _observabilidade;

    public SqlOutbox(OrcamentoPagamentoDbContext dbContext, ObservabilidadeMensagens? observabilidade = null)
    {
        _dbContext = dbContext;
        _observabilidade = observabilidade;
    }

    public async Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default)
    {
        if (_observabilidade is not null)
        {
            mensagem = mensagem with { TraceContext = _observabilidade.CapturarContexto(mensagem.TraceContext) };
        }
        await _dbContext.Outbox.AddAsync(RegistroOutbox.Criar(mensagem, versaoNegocio), cancellationToken);
    }
}
