using AutoMapper;
using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ConferirPagamento;

public sealed class ConferirPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IContextoIntegracaoPagamentoStore _contextoIntegracaoPagamentoStore;
    private readonly IProvedorPagamento _provedorPagamento;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IValidator<ConferirPagamentoRequest> _validator;
    private readonly IMapper _mapper;
    private readonly TimeProvider _relogio;

    public ConferirPagamentoUseCase(
        IPagamentoRepository pagamentoRepository,
        IContextoIntegracaoPagamentoStore contextoIntegracaoPagamentoStore,
        IProvedorPagamento provedorPagamento,
        IInbox inbox,
        IOutbox outbox,
        IValidator<ConferirPagamentoRequest> validator,
        IMapper mapper,
        TimeProvider relogio)
    {
        _pagamentoRepository = pagamentoRepository;
        _contextoIntegracaoPagamentoStore = contextoIntegracaoPagamentoStore;
        _provedorPagamento = provedorPagamento;
        _inbox = inbox;
        _outbox = outbox;
        _validator = validator;
        _mapper = mapper;
        _relogio = relogio;
    }

    public async Task<Result<PagamentoResponse>> ExecuteAsync(ConferirPagamentoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<PagamentoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        var resposta = await _provedorPagamento.ConsultarPagamentoAsync(request.IdentificadorExterno, cancellationToken);
        if (resposta.IdentificadorExterno != request.IdentificadorExterno || resposta.Moeda != "BRL")
        {
            throw new ContratoMensagemException("PAGAMENTO_PROVEDOR_INCOMPATIVEL");
        }

        Result<PagamentoResponse>? resultado = null;
        await _inbox.ExecutarEmTransacaoAsync(async ct =>
        {
            var pagamento = await _pagamentoRepository.ObterPorIdAsync(resposta.Referencia, ct);
            if (pagamento is null)
            {
                resultado = Result<PagamentoResponse>.Falha(PagamentoApplicationErrorMessages.PagamentoNaoEncontrado, TipoErro.NaoEncontrado);
                return;
            }
            if (pagamento.Valor != resposta.Valor)
            {
                throw new ContratoMensagemException("VALOR_PAGAMENTO_DIVERGENTE");
            }
            if (resposta.Status is StatusPagamentoProvedor.Desconhecido or StatusPagamentoProvedor.Estornado or StatusPagamentoProvedor.Contestado)
            {
                resultado = Result<PagamentoResponse>.Falha(PagamentoApplicationErrorMessages.ConciliacaoManualNecessaria, TipoErro.Conflito);
                return;
            }

            var contexto = await _contextoIntegracaoPagamentoStore.ObterContextoAsync(pagamento.Id, ct);
            var mensagem = ResultadosPagamento.Criar("ConferirPagamento", pagamento, contexto with { MensagemId = request.OperacaoId }, _relogio);
            var chave = $"ConferirPagamento:{resposta.IdentificadorExterno}:{resposta.Status}";
            var hash = JsonContrato.Hash(mensagem, resposta);
            if (!await _inbox.VerificarRepeticaoAsync(request.OperacaoId, chave, hash, ct))
            {
                string? tipoResultado = null;
                if (resposta.Status == StatusPagamentoProvedor.Confirmado && pagamento.Confirmar(resposta.IdentificadorExterno, resposta.Valor))
                {
                    tipoResultado = "PagamentoConfirmado";
                }
                else if (resposta.Status == StatusPagamentoProvedor.Recusado && pagamento.Recusar(resposta.IdentificadorExterno, resposta.Valor))
                {
                    tipoResultado = "PagamentoRecusado";
                }
                if (tipoResultado is not null)
                {
                    await _pagamentoRepository.AtualizarAsync(pagamento, ct);
                    await _outbox.AdicionarResultadoAsync(ResultadosPagamento.Criar(tipoResultado, pagamento,
                        contexto with { MensagemId = request.OperacaoId }, _relogio), pagamento.Versao, ct);
                }
            }
            await _inbox.RegistrarAsync(request.OperacaoId, chave, hash, ct);
            resultado = Result<PagamentoResponse>.Ok(_mapper.Map<PagamentoResponse>(pagamento));
        }, cancellationToken);

        return resultado ?? throw new InvalidOperationException("A conferência do pagamento não foi concluída.");
    }
}
