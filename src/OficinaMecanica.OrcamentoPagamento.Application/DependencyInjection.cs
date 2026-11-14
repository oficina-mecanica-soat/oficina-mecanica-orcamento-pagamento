using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos;

namespace OficinaMecanica.OrcamentoPagamento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(_ => { }, typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddOrcamentosApplication();
        services.AddPagamentosApplication();

        return services;
    }
}
