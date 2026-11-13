using System.Text.Json;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;
namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed class ProcessadorMensagemOrcamento
{
    private readonly IRecebimentoMensagemRepository _recebimentos;
    private readonly ICancelamentoMensagemRepository _cancelamentos;
    private readonly IOrcamentoRepository _orcamentos;
    private readonly IIntegracaoOrcamentoRepository _integracao;
    private readonly GerarOrcamentoUseCase _gerar;
    private readonly CancelarOrcamentoUseCase _cancelar;
    private readonly TimeProvider _relogio;
    public ProcessadorMensagemOrcamento(IRecebimentoMensagemRepository recebimentos, ICancelamentoMensagemRepository cancelamentos,
        IOrcamentoRepository orcamentos, IIntegracaoOrcamentoRepository integracao, GerarOrcamentoUseCase gerar, CancelarOrcamentoUseCase cancelar, TimeProvider relogio)
    {
        _recebimentos = recebimentos; _cancelamentos = cancelamentos; _orcamentos = orcamentos;
        _integracao = integracao; _gerar = gerar; _cancelar = cancelar; _relogio = relogio;
    }
    public async Task ProcessarAsync(string json, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(json);
        var gerar = mensagem.Tipo == "GerarOrcamento" ? JsonContrato.Ler<GerarOrcamentoDados>(mensagem.Dados.GetRawText()) : null;
        var cancelar = mensagem.Tipo == "CancelarOrcamento" ? JsonContrato.Ler<CancelarOrcamentoDados>(mensagem.Dados.GetRawText()) : null;
        if (gerar is null && cancelar is null) { throw new ContratoMensagemException("TIPO_DESCONHECIDO"); }
        await _recebimentos.ExecutarEmTransacaoAsync(async ct =>
        {
            int versao;
            object dados;
            if (gerar is not null)
            {
                var request = CriarRequest(mensagem, gerar);
                if (!(await new GerarOrcamentoValidator().ValidateAsync(request, ct)).IsValid) { throw new ContratoMensagemException("DADOS_INVALIDOS"); }
                gerar = gerar with { Servicos = gerar.Servicos.OrderBy(s => s.ServicoId).ToArray(), Itens = gerar.Itens.OrderBy(i => i.ItemId).ToArray() };
                versao = gerar.Versao; dados = gerar;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(cancelar!.Motivo) || cancelar.Motivo.Length > 160) { throw new ContratoMensagemException("MOTIVO_INVALIDO"); }
                var existente = cancelar.OrcamentoId.HasValue ? await _orcamentos.ObterPorIdAsync(cancelar.OrcamentoId.Value, ct) : null;
                if (cancelar.OrcamentoId.HasValue && (existente is null || existente.OrdemServicoId != mensagem.OrdemServicoId))
                { throw new ContratoMensagemException("ORCAMENTO_INCOMPATIVEL"); }
                versao = existente?.Versao ?? cancelar.Versao ?? 0;
                if (versao <= 0 || cancelar.Versao.HasValue && cancelar.Versao != versao) { throw new ContratoMensagemException("VERSAO_INVALIDA"); }
                dados = new { Versao = versao, Motivo = cancelar.Motivo.Trim() };
            }
            var chave = $"{mensagem.Tipo}:{mensagem.OrdemServicoId:D}:{versao}";
            var hash = JsonContrato.Hash(mensagem, dados);
            if (!await _recebimentos.VerificarRepeticaoAsync(mensagem.MensagemId, chave, hash, ct))
            {
                if (gerar is not null) { await GerarAsync(mensagem, gerar, ct); }
                else { await CancelarAsync(mensagem, cancelar!, versao, ct); }
            }
            await _recebimentos.RegistrarAsync(mensagem.MensagemId, chave, hash, ct);
        }, cancellationToken);
    }
    private async Task GerarAsync(MensagemIntegracao mensagem, GerarOrcamentoDados dados, CancellationToken ct)
    {
        var cancelamento = await _cancelamentos.ObterAsync(mensagem.OrdemServicoId, dados.Versao, ct);
        if (cancelamento is not null)
        {
            if (cancelamento.SagaId != mensagem.SagaId) { throw new ContratoMensagemException("SAGA_DIVERGENTE"); }
            return;
        }
        var resultado = await _gerar.ExecuteAsync(CriarRequest(mensagem, dados), ct);
        if (!resultado.Sucesso) { throw new ContratoMensagemException("GERACAO_REJEITADA"); }
    }
    private static GerarOrcamentoRequest CriarRequest(MensagemIntegracao mensagem, GerarOrcamentoDados dados) =>
        new(mensagem.OrdemServicoId, dados.ClienteId, dados.Versao,
            new(mensagem.SagaId, mensagem.CorrelationId, mensagem.MensagemId, mensagem.TraceContext),
            dados.Servicos?.Select(s => s is null ? null! : new ServicoOrcamentoRequest(s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor)).ToArray()!,
            dados.Itens?.Select(i => i is null ? null! : new PecaInsumoOrcamentoRequest(i.ItemId, i.Descricao, i.Quantidade, i.ValorUnitario)).ToArray()!);
    private async Task CancelarAsync(MensagemIntegracao mensagem, CancelarOrcamentoDados dados, int versao, CancellationToken ct)
    {
        var anterior = await _cancelamentos.ObterAsync(mensagem.OrdemServicoId, versao, ct);
        if (anterior is not null)
        {
            if (anterior.SagaId != mensagem.SagaId || anterior.Motivo != dados.Motivo.Trim()) { throw new ContratoMensagemException("CANCELAMENTO_DIVERGENTE"); }
            return;
        }
        var orcamento = await _orcamentos.ObterPorOrdemEVersaoAsync(mensagem.OrdemServicoId, versao, ct);
        if (orcamento is not null)
        {
            var contexto = await _integracao.ObterContextoAsync(orcamento.Id, ct);
            if (contexto.SagaId != mensagem.SagaId) { throw new ContratoMensagemException("SAGA_DIVERGENTE"); }
            var resultado = await _cancelar.ExecuteAsync(new(orcamento.Id, versao, mensagem.MensagemId, dados.Motivo), ct);
            if (!resultado.Sucesso) { throw new ContratoMensagemException("CANCELAMENTO_REJEITADO"); }
        }
        else
        {
            var resultado = new MensagemIntegracao(Guid.CreateVersion7(), "OrcamentoCancelado", 1, mensagem.SagaId, mensagem.OrdemServicoId,
                mensagem.CorrelationId, mensagem.MensagemId, _relogio.GetUtcNow(),
                JsonSerializer.SerializeToElement(new OrcamentoCanceladoDados(null, versao), JsonContrato.Options), mensagem.TraceContext);
            await _integracao.AdicionarResultadoAsync(resultado, versao, ct);
        }
        await _cancelamentos.RegistrarAsync(new(mensagem.OrdemServicoId, versao, mensagem.SagaId, dados.Motivo.Trim()), ct);
    }
}
