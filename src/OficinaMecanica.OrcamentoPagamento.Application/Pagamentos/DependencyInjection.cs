using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos;

public static class DependencyInjection
{
    public static IServiceCollection AddPagamentosApplication(this IServiceCollection services)
    {
        return services.AddPagamentoUseCases();
    }
}
