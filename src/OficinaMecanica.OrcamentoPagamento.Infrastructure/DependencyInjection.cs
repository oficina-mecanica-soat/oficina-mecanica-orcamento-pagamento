using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Repositories;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string do banco próprio de Orçamento/Pagamento não configurada.");
        }
        services.AddDbContext<OrcamentoPagamentoDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<IIntegracaoOrcamentoRepository, IntegracaoOrcamentoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
