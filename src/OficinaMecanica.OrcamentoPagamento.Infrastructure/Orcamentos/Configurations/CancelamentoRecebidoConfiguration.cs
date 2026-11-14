using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Configurations;

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
