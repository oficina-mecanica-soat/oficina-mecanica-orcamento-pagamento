using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Configurations;

public sealed class RegistroOutboxConfiguration : IEntityTypeConfiguration<RegistroOutbox>
{
    public void Configure(EntityTypeBuilder<RegistroOutbox> builder)
    {
        builder.ToTable("Outbox", "OrcamentoPagamento");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Tipo).HasMaxLength(64).IsRequired();
        builder.Property(o => o.Corpo).IsRequired();
        builder.HasIndex(o => new { o.Tipo, o.OrdemServicoId, o.VersaoNegocio }).IsUnique();
        builder.HasIndex(o => o.PublicadoEm);
    }
}
