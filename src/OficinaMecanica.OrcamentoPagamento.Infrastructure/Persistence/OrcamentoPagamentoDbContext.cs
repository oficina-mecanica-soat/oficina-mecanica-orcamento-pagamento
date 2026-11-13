using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

public sealed class OrcamentoPagamentoDbContext : DbContext
{
    public OrcamentoPagamentoDbContext(DbContextOptions<OrcamentoPagamentoDbContext> options) : base(options) { }
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<RegistroOutbox> Outbox => Set<RegistroOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrcamentoPagamentoDbContext).Assembly);
    }
}
