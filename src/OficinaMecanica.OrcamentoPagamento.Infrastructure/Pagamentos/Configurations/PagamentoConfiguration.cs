using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Configurations;

public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("Pagamentos", "OrcamentoPagamento");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Valor).HasPrecision(18, 2);
        builder.Property(p => p.Status).HasConversion<int>();
        builder.Property(p => p.CheckoutId).HasMaxLength(200);
        builder.Property(p => p.EnderecoCheckout).HasMaxLength(2048);
        builder.Property(p => p.IdentificadorPagamentoProvedor).HasMaxLength(200);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Property<Guid>("SagaId").IsRequired();
        builder.Property<Guid>("MensagemOrigemId").IsRequired();
        builder.Property<string>("CorrelationId").HasMaxLength(128).IsRequired();
        builder.Property<string>("TraceContext");
        builder.HasIndex(p => p.OrcamentoId).IsUnique();
        builder.HasIndex(p => new { p.OrdemServicoId, p.Versao }).IsUnique();
        builder.HasOne<Orcamento>().WithMany().HasForeignKey(p => p.OrcamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
