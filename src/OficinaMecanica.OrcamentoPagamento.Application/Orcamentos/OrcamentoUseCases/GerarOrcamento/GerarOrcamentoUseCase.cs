using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;

public sealed class GerarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IIntegracaoOrcamentoRepository _integracaoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<GerarOrcamentoRequest> _validator;
    private readonly IMapper _mapper;
    private readonly TimeProvider _relogio;
    private readonly ILogger<GerarOrcamentoUseCase> _logger;
    private readonly ICancelamentoMensagemRepository _cancelamentos;

    public GerarOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IIntegracaoOrcamentoRepository integracaoRepository,
        IUnitOfWork unitOfWork, IValidator<GerarOrcamentoRequest> validator, IMapper mapper,
        TimeProvider relogio, ILogger<GerarOrcamentoUseCase> logger, ICancelamentoMensagemRepository cancelamentos)
    {
        _orcamentoRepository = orcamentoRepository;
        _integracaoRepository = integracaoRepository;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _mapper = mapper;
        _relogio = relogio;
        _logger = logger;
        _cancelamentos = cancelamentos;
    }
    public async Task<Result<OrcamentoResponse>> ExecuteAsync(GerarOrcamentoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<OrcamentoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        if (await _cancelamentos.ObterAsync(request.OrdemServicoId, request.Versao, cancellationToken) is not null)
        {
            return Result<OrcamentoResponse>.Falha(OrcamentoErrorMessages.VersaoCancelada, TipoErro.RegraNegocio);
        }
        var novo = Orcamento.Gerar(request.OrdemServicoId, request.ClienteId, request.Versao,
            request.Servicos.Select(s => ServicoOrcamento.Criar(s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor)).ToArray(),
            request.PecasInsumos.Select(p => PecaInsumoOrcamento.Criar(p.PecaInsumoCatalogoId, p.Descricao, p.Quantidade, p.ValorUnitario)).ToArray());
        var existente = await _orcamentoRepository.ObterPorOrdemEVersaoAsync(request.OrdemServicoId, request.Versao, cancellationToken);
        if (existente is not null)
        {
            var contexto = await _integracaoRepository.ObterContextoAsync(existente.Id, cancellationToken);
            return existente.PossuiMesmoSnapshot(novo) && contexto.SagaId == request.Contexto.SagaId
                ? Result<OrcamentoResponse>.Ok(_mapper.Map<OrcamentoResponse>(existente))
                : Result<OrcamentoResponse>.Falha(OrcamentoErrorMessages.ResultadoDivergente, TipoErro.Conflito);
        }

        var cancelada = false;
        await _unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            // A mesma guarda dentro da transação impede a corrida com cancelamento antecipado.
            if (await _cancelamentos.ObterAsync(request.OrdemServicoId, request.Versao, ct) is not null)
            {
                cancelada = true;
                return;
            }
            await _orcamentoRepository.AdicionarAsync(novo, ct);
            await _integracaoRepository.RegistrarContextoAsync(novo.Id, request.Contexto, ct);
            await _integracaoRepository.AdicionarResultadoAsync(ResultadosOrcamento.Gerado(novo, request.Contexto, _relogio), novo.Versao, ct);
        }, cancellationToken);
        if (cancelada)
        {
            return Result<OrcamentoResponse>.Falha(OrcamentoErrorMessages.VersaoCancelada, TipoErro.RegraNegocio);
        }
        _logger.LogInformation("Orçamento {OrcamentoId} gerado para a OS {OrdemServicoId}, versão {Versao}. {operation} {bounded_context}", novo.Id, novo.OrdemServicoId, novo.Versao, "GerarOrcamento", "Orcamentos");
        return Result<OrcamentoResponse>.Ok(_mapper.Map<OrcamentoResponse>(novo));
    }
}
