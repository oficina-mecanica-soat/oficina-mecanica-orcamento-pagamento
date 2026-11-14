using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using Microsoft.Extensions.Logging;
using AutoMapper;
using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;

public sealed class CancelarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IIntegracaoOrcamentoRepository _integracaoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CancelarOrcamentoRequest> _validator;
    private readonly IMapper _mapper;
    private readonly TimeProvider _relogio;
    private readonly ILogger<CancelarOrcamentoUseCase> _logger;

    public CancelarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IIntegracaoOrcamentoRepository integracaoRepository,
        IUnitOfWork unitOfWork, IValidator<CancelarOrcamentoRequest> validator, IMapper mapper, TimeProvider relogio, ILogger<CancelarOrcamentoUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _integracaoRepository = integracaoRepository;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _mapper = mapper;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<Result<OrcamentoResponse>> ExecuteAsync(CancelarOrcamentoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<OrcamentoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(request.OrcamentoId, cancellationToken);
        if (orcamento is null)
        {
            return Result<OrcamentoResponse>.Falha(OrcamentoErrorMessages.OrcamentoNaoEncontrado, TipoErro.NaoEncontrado);
        }

        if (orcamento.Cancelar(request.Versao, request.Motivo))
        {
            var contexto = await _integracaoRepository.ObterContextoAsync(orcamento.Id, cancellationToken);
            await _unitOfWork.ExecutarEmTransacaoAsync(async ct =>
            {
                await _orcamentoRepository.AtualizarAsync(orcamento, ct);
                await _integracaoRepository.AdicionarResultadoAsync(
                    ResultadosOrcamento.Cancelado(orcamento, contexto with
                    {
                        MensagemId = request.OperacaoId
                    }, _relogio), orcamento.Versao, ct);
            }, cancellationToken);
            _logger.LogInformation("Orçamento {OrcamentoId} atualizado para {Status}. {operation} {bounded_context}",
                orcamento.Id, orcamento.Status, "CancelarOrcamento", "Orcamentos");
        }
        return Result<OrcamentoResponse>.Ok(_mapper.Map<OrcamentoResponse>(orcamento));
    }
}
