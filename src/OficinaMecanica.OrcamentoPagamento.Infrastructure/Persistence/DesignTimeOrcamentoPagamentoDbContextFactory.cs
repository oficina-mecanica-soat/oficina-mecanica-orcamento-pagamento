using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

public sealed class DesignTimeOrcamentoPagamentoDbContextFactory : IDesignTimeDbContextFactory<OrcamentoPagamentoDbContext>
{
    public OrcamentoPagamentoDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiPath = Directory.Exists(Path.Combine(currentDirectory, "src", "OficinaMecanica.OrcamentoPagamento.API"))
            ? Path.Combine(currentDirectory, "src", "OficinaMecanica.OrcamentoPagamento.API")
            : Path.Combine(currentDirectory, "..", "OficinaMecanica.OrcamentoPagamento.API");
        var configuration = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(apiPath))
            .AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection para o banco próprio.");
        }
        return new OrcamentoPagamentoDbContext(new DbContextOptionsBuilder<OrcamentoPagamentoDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}
