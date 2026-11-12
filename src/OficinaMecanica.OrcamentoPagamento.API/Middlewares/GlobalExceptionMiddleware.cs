using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Responses;
using OficinaMecanica.OrcamentoPagamento.API.Responses;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;

namespace OficinaMecanica.OrcamentoPagamento.API.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            _logger.LogWarning(exception, "Falha de regra de negócio ao processar {operation}.", "HTTP request");
            await WriteErrorAsync(context, TipoErro.RegraNegocio.ToHttpStatusCode(), new ErrorResponse(exception.Message, TipoErro.RegraNegocio));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            _logger.LogWarning(exception, "Conflito de persistência ao processar {operation}.", "HTTP request");
            await WriteErrorAsync(context, TipoErro.Conflito.ToHttpStatusCode(), new ErrorResponse(ApiResponseMessages.ConflitoPersistencia, TipoErro.Conflito));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            _logger.LogWarning(exception, "Violação de unicidade ao processar {operation}.", "HTTP request");
            await WriteErrorAsync(context, TipoErro.Conflito.ToHttpStatusCode(), new ErrorResponse(ApiResponseMessages.ConflitoPersistencia, TipoErro.Conflito));
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            _logger.LogError(exception, "Erro inesperado ao processar {operation}.", "HTTP request");
            await WriteErrorAsync(context, TipoErro.ErroInterno.ToHttpStatusCode(), new ErrorResponse(ApiResponseMessages.ErroInternoInesperado, TipoErro.ErroInterno));
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, ErrorResponse error)
    {
        context.Response.StatusCode = statusCode;

        await context.Response.WriteApiErrorResponseAsJsonAsync(error);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
