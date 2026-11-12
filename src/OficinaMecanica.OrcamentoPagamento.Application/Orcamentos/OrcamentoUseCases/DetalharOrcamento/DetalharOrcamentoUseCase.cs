using AutoMapper;
using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.DetalharOrcamento;

public sealed class DetalharOrcamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IValidator<DetalharOrcamentoRequest> _validator;
    private readonly IMapper _mapper;

    public DetalharOrcamentoUseCase(IOrcamentoRepository orcamentoRepository, IValidator<DetalharOrcamentoRequest> validator, IMapper mapper)
    {
        _orcamentoRepository = orcamentoRepository;
        _validator = validator;
        _mapper = mapper;
    }

    public async Task<Result<OrcamentoResponse>> ExecuteAsync(DetalharOrcamentoRequest request, CancellationToken cancellationToken = default)
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
        return Result<OrcamentoResponse>.Ok(_mapper.Map<OrcamentoResponse>(orcamento));
    }
}
