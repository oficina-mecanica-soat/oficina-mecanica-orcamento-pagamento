using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Responses;
using OficinaMecanica.OrcamentoPagamento.Application.Identidade;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ConferirPagamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.DetalharPagamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.Responses;

namespace OficinaMecanica.OrcamentoPagamento.API.Pagamentos.Controllers;

[ApiController]
[Route("api/v1/pagamentos")]
[Authorize]
public sealed class PagamentosController : ControllerBase
{
    [HttpGet("{pagamentoId:guid}")]
    [Authorize(Roles = PerfisAcesso.AdministradorAtendenteCliente)]
    [ProducesResponseType(typeof(PagamentoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detalhar([FromServices] DetalharPagamentoUseCase useCase, Guid pagamentoId, CancellationToken cancellationToken)
    {
        Guid? clienteId = null;
        if (User.IsInRole(PerfisAcesso.Cliente))
        {
            if (!Guid.TryParse(User.FindFirstValue("cliente_id"), out var identificador) || identificador == Guid.Empty)
            {
                return Forbid();
            }
            clienteId = identificador;
        }
        return this.ToActionResult(await useCase.ExecuteAsync(new(pagamentoId, clienteId), cancellationToken));
    }

    [HttpPost("notificacoes/mercado-pago")]
    [AllowAnonymous]
    [RequestSizeLimit(16384)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConferirNotificacao([FromServices] IValidadorNotificacaoPagamento validador,
        [FromServices] ConferirPagamentoUseCase useCase, [FromBody] JsonElement corpo, CancellationToken cancellationToken)
    {
        var pagamentoId = Request.Query["data.id"];
        var assinatura = Request.Headers["x-signature"];
        var requisicaoId = Request.Headers["x-request-id"];
        if (pagamentoId.Count != 1 || assinatura.Count != 1 || requisicaoId.Count != 1
            || !validador.Validar(assinatura[0], requisicaoId[0], pagamentoId[0]))
        {
            return Unauthorized();
        }
        if (corpo.ValueKind != JsonValueKind.Object || !corpo.TryGetProperty("type", out var tipo)
            || tipo.ValueKind != JsonValueKind.String || tipo.GetString() != "payment"
            || !corpo.TryGetProperty("data", out var dados) || dados.ValueKind != JsonValueKind.Object
            || !dados.TryGetProperty("id", out var identificador)
            || identificador.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)
            || identificador.ToString() != pagamentoId[0])
        {
            return BadRequest();
        }

        try
        {
            var resultado = await useCase.ExecuteAsync(new(pagamentoId[0]!, Guid.CreateVersion7()), cancellationToken);
            return resultado.Sucesso ? Ok() : this.ToActionResult(resultado);
        }
        catch (ProvedorPagamentoException)
        {
            // Sem consulta confiável ao provedor, não confirmar recebimento nem pagamento.
            return this.ToActionResult(Result<PagamentoResponse>.Falha(PagamentoApplicationErrorMessages.ProvedorIndisponivel, TipoErro.Indisponivel));
        }
        catch (ContratoMensagemException)
        {
            return this.ToActionResult(Result<PagamentoResponse>.Falha(PagamentoApplicationErrorMessages.NotificacaoDivergente, TipoErro.Conflito));
        }
    }
}
