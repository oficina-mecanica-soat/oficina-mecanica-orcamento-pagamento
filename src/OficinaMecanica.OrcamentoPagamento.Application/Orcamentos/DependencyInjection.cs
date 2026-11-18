using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos;

public static class DependencyInjection
{
    public static IServiceCollection AddOrcamentosApplication(this IServiceCollection services)
    {
        services.AddOrcamentoUseCases();
        return services;
    }
}
