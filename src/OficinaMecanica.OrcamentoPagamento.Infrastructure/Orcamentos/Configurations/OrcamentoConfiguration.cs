using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Configurations;

public sealed class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("Orcamentos", "OrcamentoPagamento");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Status).HasConversion<int>();
        builder.Property(o => o.Decisao).HasConversion<int?>();
        builder.Property(o => o.MotivoCancelamento).HasMaxLength(160);
        builder.Property(o => o.ValorTotal).HasPrecision(18, 2);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Property<Guid>("SagaId").IsRequired();
        builder.Property<Guid>("MensagemOrigemId").IsRequired();
        builder.Property<string>("CorrelationId").IsRequired();
        builder.HasIndex(o => new { o.OrdemServicoId, o.Versao }).IsUnique();
        builder.OwnsMany(o => o.Servicos, servico =>
        {
            servico.ToTable("ServicosOrcamento", "OrcamentoPagamento");
            servico.WithOwner().HasForeignKey("OrcamentoId");
            servico.HasKey("OrcamentoId", nameof(Domain.Orcamentos.Entities.ServicoOrcamento.ServicoId));
            servico.Property(s => s.ServicoId).ValueGeneratedNever();
            servico.Property(s => s.Descricao).HasMaxLength(RegrasSnapshot.DescricaoMaxima).IsRequired();
            servico.Property(s => s.Valor).HasPrecision(18, 2);
        });
        builder.OwnsMany(o => o.PecasInsumos, peca =>
        {
            peca.ToTable("PecasInsumosOrcamento", "OrcamentoPagamento");
            peca.WithOwner().HasForeignKey("OrcamentoId");
            peca.HasKey("OrcamentoId", nameof(Domain.Orcamentos.Entities.PecaInsumoOrcamento.PecaInsumoCatalogoId));
            peca.Property(p => p.PecaInsumoCatalogoId).ValueGeneratedNever();
            peca.Property(p => p.Descricao).HasMaxLength(RegrasSnapshot.DescricaoMaxima).IsRequired();
            peca.Property(p => p.ValorUnitario).HasPrecision(18, 2);
            peca.Ignore(p => p.ValorTotal);
        });
        builder.Navigation(o => o.Servicos).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.PecasInsumos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
