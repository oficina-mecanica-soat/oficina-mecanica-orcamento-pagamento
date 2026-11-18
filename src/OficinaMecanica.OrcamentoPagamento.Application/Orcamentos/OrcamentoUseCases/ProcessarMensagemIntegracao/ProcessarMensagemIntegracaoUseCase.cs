using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ProcessarMensagemPagamento;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemIntegracao;

public sealed class ProcessarMensagemIntegracaoUseCase : IProcessadorMensagem
{
    private readonly ProcessarMensagemOrcamentoUseCase _processarMensagemOrcamentoUseCase;
    private readonly ProcessarMensagemPagamentoUseCase _processarMensagemPagamentoUseCase;

    public ProcessarMensagemIntegracaoUseCase(
        ProcessarMensagemOrcamentoUseCase processarMensagemOrcamentoUseCase,
        ProcessarMensagemPagamentoUseCase processarMensagemPagamentoUseCase)
    {
        _processarMensagemOrcamentoUseCase = processarMensagemOrcamentoUseCase;
        _processarMensagemPagamentoUseCase = processarMensagemPagamentoUseCase;
    }

    public Task ProcessarAsync(string corpo, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(corpo);
        return mensagem.Tipo switch
        {
            "GerarOrcamento" or "CancelarOrcamento" => _processarMensagemOrcamentoUseCase.ExecuteAsync(corpo, cancellationToken),
            "CriarCobranca" => _processarMensagemPagamentoUseCase.ExecuteAsync(corpo, cancellationToken),
            _ => throw new ContratoMensagemException("TIPO_DESCONHECIDO")
        };
    }
}
