using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;

public sealed class Orcamento
{
    private readonly List<ServicoOrcamento> _servicos = [];
    private readonly List<PecaInsumoOrcamento> _pecasInsumos = [];
    private Orcamento() { }
    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public int Versao { get; private set; }
    public StatusOrcamento Status { get; private set; }
    public DecisaoOrcamento? Decisao { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public decimal ValorTotal { get; private set; }
    public IReadOnlyCollection<ServicoOrcamento> Servicos => _servicos.AsReadOnly();
    public IReadOnlyCollection<PecaInsumoOrcamento> PecasInsumos => _pecasInsumos.AsReadOnly();

    public static Orcamento Gerar(Guid ordemServicoId, Guid clienteId, int versao,
        IReadOnlyCollection<ServicoOrcamento> servicos, IReadOnlyCollection<PecaInsumoOrcamento> pecasInsumos)
    {
        RegrasSnapshot.ValidarIdentificador(ordemServicoId);
        RegrasSnapshot.ValidarIdentificador(clienteId);
        if (versao <= 0)
        {
            throw new DomainException(OrcamentoErrorMessages.VersaoInvalida);
        }
        if (servicos is null || pecasInsumos is null || servicos.Any(s => s is null) || pecasInsumos.Any(p => p is null))
        {
            throw new DomainException(OrcamentoErrorMessages.ColecaoInvalida);
        }
        if (servicos.Count == 0)
        {
            throw new DomainException(OrcamentoErrorMessages.ServicosObrigatorios);
        }
        if (servicos.Select(s => s.ServicoId).Distinct().Count() != servicos.Count
            || pecasInsumos.Select(p => p.PecaInsumoCatalogoId).Distinct().Count() != pecasInsumos.Count)
            throw new DomainException(OrcamentoErrorMessages.ReferenciasDuplicadas);

        decimal total = 0;
        foreach (var valor in servicos.Select(s => s.Valor).Concat(pecasInsumos.Select(p => p.ValorTotal)))
        {
            if (valor > RegrasSnapshot.ValorMaximo - total)
            {
                throw new DomainException(OrcamentoErrorMessages.ValorInvalido);
            }
            total += valor;
        }

        var orcamento = new Orcamento
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = ordemServicoId,
            ClienteId = clienteId,
            Versao = versao,
            Status = StatusOrcamento.AguardandoAprovacao,
            ValorTotal = total
        };
        orcamento._servicos.AddRange(servicos.OrderBy(s => s.ServicoId));
        orcamento._pecasInsumos.AddRange(pecasInsumos.OrderBy(p => p.PecaInsumoCatalogoId));
        return orcamento;
    }

    public bool PossuiMesmoSnapshot(Orcamento outro)
    {
        return OrdemServicoId == outro.OrdemServicoId && ClienteId == outro.ClienteId && Versao == outro.Versao
            && _servicos.Select(s => (s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor))
                .SequenceEqual(outro._servicos.Select(s => (s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor)))
            && _pecasInsumos.Select(p => (p.PecaInsumoCatalogoId, p.Descricao, p.Quantidade, p.ValorUnitario))
                .SequenceEqual(outro._pecasInsumos.Select(p => (p.PecaInsumoCatalogoId, p.Descricao, p.Quantidade, p.ValorUnitario)));
    }

    public bool RegistrarDecisao(int versao, DecisaoOrcamento decisao)
    {
        ExigirVersao(versao);
        if (!Enum.IsDefined(decisao))
        {
            throw new DomainException(OrcamentoErrorMessages.DecisaoInvalida);
        }
        var destino = decisao == DecisaoOrcamento.Aprovado ? StatusOrcamento.Aprovado : StatusOrcamento.Recusado;
        if (Decisao == decisao)
        {
            return false;
        }
        if (Status != StatusOrcamento.AguardandoAprovacao)
        {
            throw new DomainException(OrcamentoErrorMessages.TransicaoInvalida);
        }
        Status = destino;
        Decisao = decisao;
        return true;
    }

    public bool Cancelar(int versao, string motivo)
    {
        ExigirVersao(versao);
        var motivoValidado = RegrasSnapshot.ValidarDescricao(motivo);
        if (Status == StatusOrcamento.Cancelado)
        {
            if (MotivoCancelamento != motivoValidado)
            {
                throw new DomainException(OrcamentoErrorMessages.ResultadoDivergente);
            }
            return false;
        }
        MotivoCancelamento = motivoValidado;
        Status = StatusOrcamento.Cancelado;
        return true;
    }

    private void ExigirVersao(int versao)
    {
        if (versao != Versao)
        {
            throw new DomainException(OrcamentoErrorMessages.VersaoInvalida);
        }
    }
}
