using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.Application.Common;

namespace OficinaMecanica.OrcamentoPagamento.API.Extensions.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExigirFluxoLegadoAttribute : TypeFilterAttribute
{
    public ExigirFluxoLegadoAttribute() : base(typeof(FluxoLegadoFilter))
    {
    }
}

internal sealed class FluxoLegadoFilter : IActionFilter
{
    private readonly FluxoDistribuidoOptions _options;

    public FluxoLegadoFilter(IOptions<FluxoDistribuidoOptions> options)
    {
        _options = options.Value;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (_options.Habilitado)
        {
            context.Result = new ConflictObjectResult(new ErrorResponse("Esta operação é coordenada pela ordem de serviço no fluxo distribuído.", TipoErro.Conflito));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
