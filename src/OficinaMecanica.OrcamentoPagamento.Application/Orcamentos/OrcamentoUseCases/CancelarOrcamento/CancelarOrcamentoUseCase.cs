using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;

public sealed class CancelarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IContextoIntegracaoOrcamentoStore _contextoIntegracaoStore;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CancelarOrcamentoRequest> _validator;
    private readonly IMapper _mapper;
    private readonly TimeProvider _relogio;
    private readonly ILogger<CancelarOrcamentoUseCase> _logger;

    public CancelarOrcamentoUseCase(
        IOrcamentoRepository orcamentoRepository,
        IPagamentoRepository pagamentoRepository,
        IContextoIntegracaoOrcamentoStore integracaoRepository,
        IOutbox outbox,
        IUnitOfWork unitOfWork,
        IValidator<CancelarOrcamentoRequest> validator,
        IMapper mapper,
        TimeProvider relogio,
        ILogger<CancelarOrcamentoUseCase> logger)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
        _contextoIntegracaoStore = integracaoRepository;
        _outbox = outbox;
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
        Result<OrcamentoResponse>? resultado = null;
        await _unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var orcamento = await _orcamentoRepository.ObterPorIdAsync(request.OrcamentoId, ct);
            if (orcamento is null)
            {
                resultado = Result<OrcamentoResponse>.Falha(OrcamentoErrorMessages.OrcamentoNaoEncontrado, TipoErro.NaoEncontrado);
                return;
            }
            if (await _pagamentoRepository.ObterPorOrcamentoAsync(orcamento.Id, ct) is not null)
            {
                resultado = Result<OrcamentoResponse>.Falha("O orçamento possui cobrança solicitada e não pode ser compensado por cancelamento.", TipoErro.Conflito);
                return;
            }
            if (orcamento.Cancelar(request.Versao, request.Motivo))
            {
                var contexto = await _contextoIntegracaoStore.ObterContextoAsync(orcamento.Id, ct);
                await _orcamentoRepository.AtualizarAsync(orcamento, ct);
                await _outbox.AdicionarResultadoAsync(
                    ResultadosOrcamento.Cancelado(orcamento, contexto with
                    {
                        MensagemId = request.OperacaoId
                    }, _relogio), orcamento.Versao, ct);
                _logger.LogInformation("Orçamento {OrcamentoId} atualizado para {Status}. {operation} {bounded_context}",
                    orcamento.Id, orcamento.Status, "CancelarOrcamento", "Orcamentos");
            }
            resultado = Result<OrcamentoResponse>.Ok(_mapper.Map<OrcamentoResponse>(orcamento));
        }, cancellationToken);
        return resultado ?? throw new InvalidOperationException("O cancelamento do orçamento não foi concluído.");
    }
}
