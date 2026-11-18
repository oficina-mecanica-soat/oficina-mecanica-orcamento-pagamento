using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.SolicitarCobranca;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ProcessarMensagemPagamento;

public sealed class ProcessarMensagemPagamentoUseCase
{
    private readonly SolicitarCobrancaUseCase _solicitarCobrancaUseCase;

    public ProcessarMensagemPagamentoUseCase(SolicitarCobrancaUseCase solicitarCobrancaUseCase)
    {
        _solicitarCobrancaUseCase = solicitarCobrancaUseCase;
    }

    public async Task ExecuteAsync(string corpo, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(corpo);
        if (mensagem.Tipo != "CriarCobranca")
        {
            throw new ContratoMensagemException("TIPO_MENSAGEM_INVALIDO");
        }
        var dados = JsonContrato.Ler<CriarCobrancaDados>(mensagem.Dados.GetRawText());
        var resultado = await _solicitarCobrancaUseCase.ExecuteAsync(new(mensagem.OrdemServicoId,
            dados.OrcamentoId, dados.Versao, new ContextoIntegracao(mensagem.SagaId, mensagem.CorrelationId,
                mensagem.MensagemId, mensagem.TraceContext)), cancellationToken);
        if (!resultado.Sucesso)
        {
            throw new ContratoMensagemException("DADOS_COBRANCA_INVALIDOS");
        }
        if (resultado.Valor!.Status is nameof(StatusPagamento.Solicitado) or nameof(StatusPagamento.CriacaoEmAndamento))
        {
            throw new InvalidOperationException("Cobrança pendente de conciliação; o comando não deve ser confirmado no transporte.");
        }
    }
}
