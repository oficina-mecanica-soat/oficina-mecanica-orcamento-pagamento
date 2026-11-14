using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Observabilidade;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Orcamentos.Repositories;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos;
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
        services.AddScoped<IContextoIntegracaoOrcamentoStore, ContextoIntegracaoOrcamentoStore>();
        services.TryAddScoped(provider => new ObservabilidadeMensagens(
            provider.GetService<ILogger<ObservabilidadeMensagens>>() ?? NullLogger<ObservabilidadeMensagens>.Instance));
        services.AddScoped<IOutbox, SqlOutbox>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IInbox, SqlInbox>();
        services.AddScoped<ICancelamentoMensagemStore, CancelamentoMensagemStore>();
        services.AddPagamentosInfrastructure(configuration);

        return services;
    }
}
