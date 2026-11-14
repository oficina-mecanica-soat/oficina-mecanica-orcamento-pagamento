using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ConferirPagamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.DetalharPagamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.SolicitarCobranca;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ProcessarMensagemPagamento;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases;

public static class DependencyInjection
{
    public static IServiceCollection AddPagamentoUseCases(this IServiceCollection services)
    {
        services.AddScoped<SolicitarCobrancaUseCase>();
        services.AddScoped<ProcessarMensagemPagamentoUseCase>();
        services.AddScoped<IValidator<SolicitarCobrancaRequest>, SolicitarCobrancaValidator>();
        services.AddScoped<ConferirPagamentoUseCase>();
        services.AddScoped<IValidator<ConferirPagamentoRequest>, ConferirPagamentoValidator>();
        services.AddScoped<DetalharPagamentoUseCase>();
        services.AddScoped<IValidator<DetalharPagamentoRequest>, DetalharPagamentoValidator>();
        return services;
    }
}
