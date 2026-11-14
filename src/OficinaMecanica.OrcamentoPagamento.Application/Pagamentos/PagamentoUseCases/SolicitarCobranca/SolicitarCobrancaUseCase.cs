using AutoMapper;
using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.SolicitarCobranca;

public sealed class SolicitarCobrancaUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IContextoIntegracaoOrcamentoStore _contextoIntegracaoOrcamentoStore;
    private readonly IContextoIntegracaoPagamentoStore _contextoIntegracaoPagamentoStore;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProvedorPagamento _provedorPagamento;
    private readonly IValidator<SolicitarCobrancaRequest> _validator;
    private readonly IMapper _mapper;
    private readonly TimeProvider _relogio;

    public SolicitarCobrancaUseCase(
        IOrcamentoRepository orcamentoRepository,
        IPagamentoRepository pagamentoRepository,
        IContextoIntegracaoOrcamentoStore contextoIntegracaoOrcamentoStore,
        IContextoIntegracaoPagamentoStore contextoIntegracaoPagamentoStore,
        IInbox inbox,
        IOutbox outbox,
        IUnitOfWork unitOfWork,
        IProvedorPagamento provedorPagamento,
        IValidator<SolicitarCobrancaRequest> validator,
        IMapper mapper,
        TimeProvider relogio)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
        _contextoIntegracaoOrcamentoStore = contextoIntegracaoOrcamentoStore;
        _contextoIntegracaoPagamentoStore = contextoIntegracaoPagamentoStore;
        _inbox = inbox;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _provedorPagamento = provedorPagamento;
        _validator = validator;
        _mapper = mapper;
        _relogio = relogio;
    }

    public async Task<Result<PagamentoResponse>> ExecuteAsync(SolicitarCobrancaRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<PagamentoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        Pagamento? pagamento = null;
        await _inbox.ExecutarEmTransacaoAsync(async ct =>
        {
            var orcamento = await _orcamentoRepository.ObterPorIdAsync(request.OrcamentoId, ct)
                ?? throw new ContratoMensagemException("ORCAMENTO_NAO_ENCONTRADO");
            var contexto = await _contextoIntegracaoOrcamentoStore.ObterContextoAsync(orcamento.Id, ct);
            if (orcamento.OrdemServicoId != request.OrdemServicoId || orcamento.Versao != request.Versao
                || orcamento.Status != StatusOrcamento.Aprovado || contexto.SagaId != request.Contexto.SagaId
                || contexto.CorrelationId != request.Contexto.CorrelationId)
            {
                throw new ContratoMensagemException("COBRANCA_INCOMPATIVEL_COM_ORCAMENTO");
            }

            var mensagem = new MensagemIntegracao(request.Contexto.MensagemId, "CriarCobranca", 1, contexto.SagaId,
                request.OrdemServicoId, contexto.CorrelationId, request.Contexto.MensagemId, _relogio.GetUtcNow(),
                System.Text.Json.JsonSerializer.SerializeToElement(new CriarCobrancaDados(request.OrcamentoId, request.Versao), JsonContrato.Options));
            var hash = JsonContrato.Hash(mensagem, new CriarCobrancaDados(request.OrcamentoId, request.Versao));
            var chave = $"CriarCobranca:{request.OrdemServicoId:D}:{request.Versao}";
            await _inbox.VerificarRepeticaoAsync(request.Contexto.MensagemId, chave, hash, ct);
            pagamento = await _pagamentoRepository.ObterPorOrcamentoAsync(orcamento.Id, ct);
            if (pagamento is null)
            {
                pagamento = Pagamento.Solicitar(orcamento.OrdemServicoId, orcamento.Id, orcamento.ClienteId, orcamento.Versao, orcamento.ValorTotal);
                await _pagamentoRepository.AdicionarAsync(pagamento, ct);
                await _contextoIntegracaoPagamentoStore.RegistrarContextoAsync(pagamento.Id, request.Contexto, ct);
            }
            await _inbox.RegistrarAsync(request.Contexto.MensagemId, chave, hash, ct);
        }, cancellationToken);

        var cobranca = pagamento ?? throw new InvalidOperationException("A intenção de cobrança não foi persistida.");
        if (cobranca.CheckoutId is not null || cobranca.Status == StatusPagamento.Confirmado)
        {
            return Result<PagamentoResponse>.Ok(_mapper.Map<PagamentoResponse>(cobranca));
        }

        // A consulta e a criação externas nunca participam da transação SQL.
        var checkout = await _provedorPagamento.ConsultarCheckoutPorReferenciaAsync(cobranca.Id, cancellationToken);
        if (checkout is null && cobranca.Status == StatusPagamento.Solicitado)
        {
            await _unitOfWork.ExecutarEmTransacaoAsync(async ct =>
            {
                cobranca.IniciarCriacao();
                await _pagamentoRepository.AtualizarAsync(cobranca, ct);
            }, cancellationToken);
            // A intenção confirmada acima impede outro POST em caso de resultado incerto.
            checkout = await _provedorPagamento.CriarCheckoutAsync(
                new CriarCheckoutRequest(cobranca.Id, $"Ordem de serviço {cobranca.OrdemServicoId:D}", cobranca.Valor), cancellationToken);
        }
        if (checkout is not null)
        {
            await RegistrarCheckoutAsync(cobranca, checkout, cancellationToken);
        }

        return Result<PagamentoResponse>.Ok(_mapper.Map<PagamentoResponse>(cobranca));
    }

    private async Task RegistrarCheckoutAsync(Pagamento pagamento, CheckoutResponse checkout, CancellationToken cancellationToken)
    {
        if (checkout.Referencia != pagamento.Id)
        {
            throw new ContratoMensagemException("REFERENCIA_CHECKOUT_DIVERGENTE");
        }
        await _unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            if (pagamento.RegistrarCheckout(checkout.IdentificadorExterno, checkout.Endereco, checkout.Valor))
            {
                var contexto = await _contextoIntegracaoPagamentoStore.ObterContextoAsync(pagamento.Id, ct);
                await _pagamentoRepository.AtualizarAsync(pagamento, ct);
                await _outbox.AdicionarResultadoAsync(ResultadosPagamento.Criar("CobrancaCriada", pagamento, contexto, _relogio), pagamento.Versao, ct);
            }
        }, cancellationToken);
    }
}
