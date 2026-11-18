using AutoMapper;
using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.DetalharPagamento;

public sealed class DetalharPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IValidator<DetalharPagamentoRequest> _validator;
    private readonly IMapper _mapper;

    public DetalharPagamentoUseCase(
        IPagamentoRepository pagamentoRepository,
        IValidator<DetalharPagamentoRequest> validator,
        IMapper mapper)
    {
        _pagamentoRepository = pagamentoRepository;
        _validator = validator;
        _mapper = mapper;
    }

    public async Task<Result<PagamentoResponse>> ExecuteAsync(DetalharPagamentoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<PagamentoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }
        var pagamento = await _pagamentoRepository.ObterPorIdAsync(request.PagamentoId, cancellationToken);
        if (pagamento is null || request.ClienteId.HasValue && pagamento.ClienteId != request.ClienteId)
        {
            return Result<PagamentoResponse>.Falha(PagamentoApplicationErrorMessages.PagamentoNaoEncontrado, TipoErro.NaoEncontrado);
        }
        return Result<PagamentoResponse>.Ok(_mapper.Map<PagamentoResponse>(pagamento));
    }
}
