using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos;

namespace OficinaMecanica.OrcamentoPagamento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(_ => { }, typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddOrcamentosApplication();
        services.AddScoped<Mensagens.ProcessadorMensagemOrcamento>();
        return services;
    }
}
