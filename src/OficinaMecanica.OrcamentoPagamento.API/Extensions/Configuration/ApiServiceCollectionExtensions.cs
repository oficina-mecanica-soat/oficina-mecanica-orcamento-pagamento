using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Security;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Swagger;
using OficinaMecanica.OrcamentoPagamento.Application.Common;

namespace OficinaMecanica.OrcamentoPagamento.API.Extensions.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddHealthChecks();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => options.AddJwtSwagger());
        services.AddJwtAuthentication(configuration);


        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var erros = context.ModelState
                    .SelectMany(item => item.Value?.Errors ?? [])
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? ValidationErrorMessages.RequestInvalido
                        : error.ErrorMessage)
                    .Distinct()
                    .ToArray();

                return new BadRequestObjectResult(new ErrorResponse(ValidationErrorMessages.RequestInvalido, TipoErro.Validacao, erros));
            };
        });

        return services;
    }
}
