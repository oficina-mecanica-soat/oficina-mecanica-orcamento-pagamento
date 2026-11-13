using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;
namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class CancelamentoMensagemRepository : ICancelamentoMensagemRepository
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    public CancelamentoMensagemRepository(OrcamentoPagamentoDbContext dbContext) { _dbContext = dbContext; }
    public Task<CancelamentoRecebido?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken) =>
        _dbContext.Set<CancelamentoRecebido>().SingleOrDefaultAsync(c => c.OrdemServicoId == ordemServicoId && c.Versao == versao, cancellationToken);
    public async Task RegistrarAsync(CancelamentoRecebido cancelamento, CancellationToken cancellationToken) =>
        await _dbContext.AddAsync(cancelamento, cancellationToken);
}
public sealed class CancelamentoRecebidoConfiguration : IEntityTypeConfiguration<CancelamentoRecebido>
{
    public void Configure(EntityTypeBuilder<CancelamentoRecebido> builder)
    {
        builder.ToTable("CancelamentosRecebidos", "OrcamentoPagamento");
        builder.HasKey(c => new { c.OrdemServicoId, c.Versao });
        builder.Property(c => c.OrdemServicoId).ValueGeneratedNever();
        builder.Property(c => c.Motivo).HasMaxLength(160).IsRequired();
    }
}
