using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemIntegracao;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.DetalharOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases;

public static class DependencyInjection
{
    public static IServiceCollection AddOrcamentoUseCases(this IServiceCollection services)
    {
        services.AddScoped<IValidator<GerarOrcamentoRequest>, GerarOrcamentoValidator>();
        services.AddScoped<IValidator<DetalharOrcamentoRequest>, DetalharOrcamentoValidator>();
        services.AddScoped<IValidator<RegistrarDecisaoOrcamentoRequest>, RegistrarDecisaoOrcamentoValidator>();
        services.AddScoped<IValidator<CancelarOrcamentoRequest>, CancelarOrcamentoValidator>();
        services.AddScoped<GerarOrcamentoUseCase>();
        services.AddScoped<DetalharOrcamentoUseCase>();
        services.AddScoped<RegistrarDecisaoOrcamentoUseCase>();
        services.AddScoped<CancelarOrcamentoUseCase>();
        services.AddScoped<ProcessarMensagemOrcamentoUseCase>();
        services.AddScoped<IProcessadorMensagem, ProcessarMensagemIntegracaoUseCase>();

        return services;
    }
}
