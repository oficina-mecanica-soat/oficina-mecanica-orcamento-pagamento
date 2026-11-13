using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class RecebimentoMensagem
{
    public Guid Id { get; init; }
    public string ChaveOperacao { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
}
public sealed class RecebimentoMensagemConfiguration : IEntityTypeConfiguration<RecebimentoMensagem>
{
    public void Configure(EntityTypeBuilder<RecebimentoMensagem> builder)
    {
        builder.ToTable("RecebimentosMensagem", "OrcamentoPagamento");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ChaveOperacao).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Hash).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.ChaveOperacao);
    }
}
