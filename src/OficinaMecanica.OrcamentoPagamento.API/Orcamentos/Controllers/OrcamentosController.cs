using System.Security.Claims;
using OficinaMecanica.OrcamentoPagamento.Application.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Responses;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.DetalharOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;

namespace OficinaMecanica.OrcamentoPagamento.API.Orcamentos.Controllers;

[ApiController]
[Route("api/v1/orcamentos")]
[Authorize]
public sealed class OrcamentosController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status201Created)]
    [Authorize(Roles = PerfisAcesso.AdministradorAtendente)]
    public async Task<IActionResult> Gerar([FromServices] GerarOrcamentoUseCase useCase, [FromBody] GerarOrcamentoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await useCase.ExecuteAsync(request, cancellationToken);
        return this.ToCreatedAtActionResult(resultado, nameof(Detalhar), valor => new { orcamentoId = valor.Id });
    }

    [HttpGet("{orcamentoId:guid}")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [Authorize(Roles = PerfisAcesso.AdministradorAtendenteCliente)]
    public async Task<IActionResult> Detalhar([FromServices] DetalharOrcamentoUseCase useCase, Guid orcamentoId, CancellationToken cancellationToken)
    {
        var clienteId = ObterClienteAutenticado();
        if (clienteId == Guid.Empty)
        {
            return Forbid();
        }
        return this.ToActionResult(await useCase.ExecuteAsync(new(orcamentoId, clienteId), cancellationToken));
    }

    [HttpPost("{orcamentoId:guid}/decisao")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status202Accepted)]
    [Authorize(Roles = PerfisAcesso.AdministradorAtendenteCliente)]
    public async Task<IActionResult> RegistrarDecisao([FromServices] RegistrarDecisaoOrcamentoUseCase useCase, Guid orcamentoId, [FromBody] DecisaoOrcamentoBody body, CancellationToken cancellationToken)
    {
        var clienteId = ObterClienteAutenticado();
        if (clienteId == Guid.Empty)
        {
            return Forbid();
        }
        var resultado = await useCase.ExecuteAsync(
            new(orcamentoId, body.Versao, body.Decisao, body.OperacaoId, clienteId), cancellationToken);
        return resultado.Sucesso ? AcceptedAtAction(nameof(Detalhar), new { orcamentoId }, resultado.Valor) : this.ToActionResult(resultado);
    }

    [HttpPost("{orcamentoId:guid}/cancelar")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status202Accepted)]
    [Authorize(Roles = PerfisAcesso.AdministradorAtendente)]
    public async Task<IActionResult> Cancelar([FromServices] CancelarOrcamentoUseCase useCase, Guid orcamentoId, [FromBody] CancelarOrcamentoBody body, CancellationToken cancellationToken)
    {
        var resultado = await useCase.ExecuteAsync(new(orcamentoId, body.Versao, body.OperacaoId, body.Motivo), cancellationToken);
        return resultado.Sucesso ? AcceptedAtAction(nameof(Detalhar), new { orcamentoId }, resultado.Valor) : this.ToActionResult(resultado);
    }

    private Guid? ObterClienteAutenticado()
    {
        if (!User.IsInRole(PerfisAcesso.Cliente))
        {
            return null;
        }
        return Guid.TryParse(User.FindFirstValue("cliente_id"), out var id) && id != Guid.Empty ? id : Guid.Empty;
    }
}

public sealed record DecisaoOrcamentoBody(int Versao, DecisaoOrcamento Decisao, Guid OperacaoId);
public sealed record CancelarOrcamentoBody(int Versao, Guid OperacaoId, string Motivo);
