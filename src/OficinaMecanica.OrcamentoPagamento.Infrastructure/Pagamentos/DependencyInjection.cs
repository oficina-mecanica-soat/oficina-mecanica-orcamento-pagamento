using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Repositories;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Services;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos;

public static class DependencyInjection
{
    public static IServiceCollection AddPagamentosInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IContextoIntegracaoPagamentoStore, ContextoIntegracaoPagamentoStore>();
        services.AddOptions<MercadoPagoOptions>()
            .Bind(configuration.GetSection(MercadoPagoOptions.SectionName))
            .Validate(options => !options.Ativo || options.PossuiConfiguracaoValida(), "Configuração do Mercado Pago incompleta ou inválida.")
            .ValidateOnStart();
        services.AddHttpClient<IProvedorPagamento, MercadoPagoClient>(httpClient =>
        {
            httpClient.BaseAddress = MercadoPagoClient.EnderecoApi;
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            httpClient.MaxResponseContentBufferSize = 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IValidadorNotificacaoPagamento, ValidadorAssinaturaMercadoPago>();

        return services;
    }
}
