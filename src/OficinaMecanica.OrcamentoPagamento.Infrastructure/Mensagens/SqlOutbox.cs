using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class SqlOutbox : IOutbox
{
    private readonly OrcamentoPagamentoDbContext _dbContext;

    public SqlOutbox(OrcamentoPagamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AdicionarResultadoAsync(MensagemIntegracao mensagem, int versaoNegocio, CancellationToken cancellationToken = default)
    {
        await _dbContext.Outbox.AddAsync(RegistroOutbox.Criar(mensagem, versaoNegocio), cancellationToken);
    }
}
