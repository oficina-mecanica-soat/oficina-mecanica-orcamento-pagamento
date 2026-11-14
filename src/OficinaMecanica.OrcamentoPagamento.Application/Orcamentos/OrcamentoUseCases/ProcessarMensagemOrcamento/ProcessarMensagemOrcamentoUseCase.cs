using System.Text.Json;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;

public sealed class ProcessarMensagemOrcamentoUseCase
{
    private readonly IRecebimentoMensagemRepository _recebimentoMensagemRepository;
    private readonly ICancelamentoMensagemRepository _cancelamentoMensagemRepository;
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IIntegracaoOrcamentoRepository _integracaoOrcamentoRepository;
    private readonly GerarOrcamentoUseCase _gerarOrcamentoUseCase;
    private readonly CancelarOrcamentoUseCase _cancelarOrcamentoUseCase;
    private readonly TimeProvider _relogio;

    public ProcessarMensagemOrcamentoUseCase(IRecebimentoMensagemRepository recebimentos, ICancelamentoMensagemRepository cancelamentos,
        IOrcamentoRepository orcamentos, IIntegracaoOrcamentoRepository integracao, GerarOrcamentoUseCase gerar, CancelarOrcamentoUseCase cancelar, TimeProvider relogio)
    {
        _recebimentoMensagemRepository = recebimentos;
        _cancelamentoMensagemRepository = cancelamentos;
        _orcamentoRepository = orcamentos;
        _integracaoOrcamentoRepository = integracao;
        _gerarOrcamentoUseCase = gerar;
        _cancelarOrcamentoUseCase = cancelar;
        _relogio = relogio;
    }

    public async Task ExecuteAsync(string json, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(json);
        var gerar = mensagem.Tipo == "GerarOrcamento" ? JsonContrato.Ler<GerarOrcamentoDados>(mensagem.Dados.GetRawText()) : null;
        var cancelar = mensagem.Tipo == "CancelarOrcamento" ? JsonContrato.Ler<CancelarOrcamentoDados>(mensagem.Dados.GetRawText()) : null;
        if (gerar is null && cancelar is null)
        {
            throw new ContratoMensagemException("TIPO_DESCONHECIDO");
        }
        await _recebimentoMensagemRepository.ExecutarEmTransacaoAsync(async cancellationTokenTransacao =>
        {
            int versao;
            object dados;
            if (gerar is not null)
            {
                var request = CriarRequest(mensagem, gerar);
                if (!(await new GerarOrcamentoValidator().ValidateAsync(request, cancellationTokenTransacao)).IsValid)
                {
                    throw new ContratoMensagemException("DADOS_INVALIDOS");
                }
                gerar = gerar with
                {
                    Servicos = gerar.Servicos.OrderBy(s => s.ServicoId).ToArray(),
                    Itens = gerar.Itens.OrderBy(i => i.ItemId).ThenBy(i => i.Descricao, StringComparer.Ordinal).ThenBy(i => i.ValorUnitario).ThenBy(i => i.Quantidade).ToArray()
                };
                versao = gerar.Versao;
                dados = gerar;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(cancelar!.Motivo) || cancelar.Motivo.Length > 160)
                {
                    throw new ContratoMensagemException("MOTIVO_INVALIDO");
                }
                var existente = cancelar.OrcamentoId.HasValue ? await _orcamentoRepository.ObterPorIdAsync(cancelar.OrcamentoId.Value, cancellationTokenTransacao) : null;
                if (cancelar.OrcamentoId.HasValue && (existente is null || existente.OrdemServicoId != mensagem.OrdemServicoId))
                {
                    throw new ContratoMensagemException("ORCAMENTO_INCOMPATIVEL");
                }
                versao = existente?.Versao ?? cancelar.Versao ?? 0;
                if (versao <= 0 || cancelar.Versao.HasValue && cancelar.Versao != versao)
                {
                    throw new ContratoMensagemException("VERSAO_INVALIDA");
                }
                dados = new
                {
                    Versao = versao,
                    Motivo = cancelar.Motivo.Trim()
                };
            }
            var chave = $"{mensagem.Tipo}:{mensagem.OrdemServicoId:D}:{versao}";
            var hash = JsonContrato.Hash(mensagem, dados);
            if (!await _recebimentoMensagemRepository.VerificarRepeticaoAsync(mensagem.MensagemId, chave, hash, cancellationTokenTransacao))
            {
                if (gerar is not null)
                {
                    await GerarAsync(mensagem, gerar, cancellationTokenTransacao);
                }
                else
                {
                    await CancelarAsync(mensagem, cancelar!, versao, cancellationTokenTransacao);
                }
            }
            await _recebimentoMensagemRepository.RegistrarAsync(mensagem.MensagemId, chave, hash, cancellationTokenTransacao);
        }, cancellationToken);
    }

    private async Task GerarAsync(MensagemIntegracao mensagem, GerarOrcamentoDados dados, CancellationToken cancellationTokenTransacao)
    {
        var cancelamento = await _cancelamentoMensagemRepository.ObterAsync(mensagem.OrdemServicoId, dados.Versao, cancellationTokenTransacao);
        if (cancelamento is not null)
        {
            if (cancelamento.SagaId != mensagem.SagaId)
            {
                throw new ContratoMensagemException("SAGA_DIVERGENTE");
            }
            return;
        }
        var resultado = await _gerarOrcamentoUseCase.ExecuteAsync(CriarRequest(mensagem, dados), cancellationTokenTransacao);
        if (!resultado.Sucesso)
        {
            throw new ContratoMensagemException("GERACAO_REJEITADA");
        }
    }

    private static GerarOrcamentoRequest CriarRequest(MensagemIntegracao mensagem, GerarOrcamentoDados dados) =>
        new(mensagem.OrdemServicoId, dados.ClienteId, dados.Versao,
            new(mensagem.SagaId, mensagem.CorrelationId, mensagem.MensagemId, mensagem.TraceContext),
            dados.Servicos?.Select(s => s is null ? null! : new ServicoOrcamentoRequest(s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor)).ToArray()!,
            dados.Itens?.Select(i => i is null ? null! : new PecaInsumoOrcamentoRequest(i.ItemId, i.Descricao, i.Quantidade, i.ValorUnitario)).ToArray()!);
    private async Task CancelarAsync(MensagemIntegracao mensagem, CancelarOrcamentoDados dados, int versao, CancellationToken cancellationTokenTransacao)
    {
        var anterior = await _cancelamentoMensagemRepository.ObterAsync(mensagem.OrdemServicoId, versao, cancellationTokenTransacao);
        if (anterior is not null)
        {
            if (anterior.SagaId != mensagem.SagaId || anterior.Motivo != dados.Motivo.Trim())
            {
                throw new ContratoMensagemException("CANCELAMENTO_DIVERGENTE");
            }
            return;
        }
        var orcamento = await _orcamentoRepository.ObterPorOrdemEVersaoAsync(mensagem.OrdemServicoId, versao, cancellationTokenTransacao);
        if (orcamento is not null)
        {
            var contexto = await _integracaoOrcamentoRepository.ObterContextoAsync(orcamento.Id, cancellationTokenTransacao);
            if (contexto.SagaId != mensagem.SagaId)
            {
                throw new ContratoMensagemException("SAGA_DIVERGENTE");
            }
            var resultado = await _cancelarOrcamentoUseCase.ExecuteAsync(new(orcamento.Id, versao, mensagem.MensagemId, dados.Motivo), cancellationTokenTransacao);
            if (!resultado.Sucesso)
            {
                throw new ContratoMensagemException("CANCELAMENTO_REJEITADO");
            }
        }
        else
        {
            var resultado = new MensagemIntegracao(Guid.CreateVersion7(), "OrcamentoCancelado", 1, mensagem.SagaId, mensagem.OrdemServicoId,
                mensagem.CorrelationId, mensagem.MensagemId, _relogio.GetUtcNow(),
                JsonSerializer.SerializeToElement(new OrcamentoCanceladoDados(null, versao), JsonContrato.Options), mensagem.TraceContext);
            await _integracaoOrcamentoRepository.AdicionarResultadoAsync(resultado, versao, cancellationTokenTransacao);
        }
        await _cancelamentoMensagemRepository.RegistrarAsync(new(mensagem.OrdemServicoId, versao, mensagem.SagaId, dados.Motivo.Trim()), cancellationTokenTransacao);
    }
}
