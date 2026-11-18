using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;

public sealed class RegistrarDecisaoOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IContextoIntegracaoOrcamentoStore _contextoIntegracaoStore;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegistrarDecisaoOrcamentoRequest> _validator;
    private readonly IMapper _mapper;
    private readonly TimeProvider _relogio;
    private readonly ILogger<RegistrarDecisaoOrcamentoUseCase> _logger;

    public RegistrarDecisaoOrcamentoUseCase(
        IOrcamentoRepository orcamentoRepository,
        IContextoIntegracaoOrcamentoStore integracaoRepository,
        IOutbox outbox,
        IUnitOfWork unitOfWork,
        IValidator<RegistrarDecisaoOrcamentoRequest> validator,
        IMapper mapper,
        TimeProvider relogio,
        ILogger<RegistrarDecisaoOrcamentoUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _contextoIntegracaoStore = integracaoRepository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _mapper = mapper;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<Result<OrcamentoResponse>> ExecuteAsync(RegistrarDecisaoOrcamentoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<OrcamentoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(request.OrcamentoId, cancellationToken);
        if (orcamento is null || request.ClienteId.HasValue && request.ClienteId != orcamento.ClienteId)
        {
            return Result<OrcamentoResponse>.Falha(OrcamentoErrorMessages.OrcamentoNaoEncontrado, TipoErro.NaoEncontrado);
        }

        if (orcamento.RegistrarDecisao(request.Versao, request.Decisao))
        {
            var contexto = await _contextoIntegracaoStore.ObterContextoAsync(orcamento.Id, cancellationToken);
            await _unitOfWork.ExecutarEmTransacaoAsync(async ct =>
            {
                await _orcamentoRepository.AtualizarAsync(orcamento, ct);
                await _outbox.AdicionarResultadoAsync(
                    ResultadosOrcamento.Decidido(orcamento, contexto with
                    {
                        MensagemId = request.OperacaoId
                    }, _relogio), orcamento.Versao, ct);
            }, cancellationToken);
            _logger.LogInformation("Orçamento {OrcamentoId} atualizado para {Status}. {operation} {bounded_context}",
                orcamento.Id, orcamento.Status, "RegistrarDecisaoOrcamento", "Orcamentos");
        }
        return Result<OrcamentoResponse>.Ok(_mapper.Map<OrcamentoResponse>(orcamento));
    }
}
