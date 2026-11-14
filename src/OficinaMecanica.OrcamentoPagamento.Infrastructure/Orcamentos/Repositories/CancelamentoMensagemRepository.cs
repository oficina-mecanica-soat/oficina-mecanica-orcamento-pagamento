using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Repositories;

public sealed class CancelamentoMensagemRepository : ICancelamentoMensagemRepository
{
    private readonly OrcamentoPagamentoDbContext _dbContext;

    public CancelamentoMensagemRepository(OrcamentoPagamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<CancelamentoRecebido?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken) =>
        _dbContext.Set<CancelamentoRecebido>().SingleOrDefaultAsync(c => c.OrdemServicoId == ordemServicoId && c.Versao == versao, cancellationToken);
    public async Task RegistrarAsync(CancelamentoRecebido cancelamento, CancellationToken cancellationToken) =>
        await _dbContext.AddAsync(cancelamento, cancellationToken);
}
