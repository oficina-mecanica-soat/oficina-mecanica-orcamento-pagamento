using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;

public sealed class ServicoOrcamento
{
    private ServicoOrcamento() { Descricao = string.Empty; }
    public Guid ServicoId { get; private set; }
    public Guid ServicoCatalogoId { get; private set; }
    public string Descricao { get; private set; }
    public decimal Valor { get; private set; }

    public static ServicoOrcamento Criar(Guid servicoId, Guid servicoCatalogoId, string descricao, decimal valor)
    {
        RegrasSnapshot.ValidarIdentificador(servicoId);
        RegrasSnapshot.ValidarIdentificador(servicoCatalogoId);
        var descricaoValidada = RegrasSnapshot.ValidarDescricao(descricao);
        RegrasSnapshot.ValidarValor(valor);
        return new ServicoOrcamento
        {
            ServicoId = servicoId,
            ServicoCatalogoId = servicoCatalogoId,
            Descricao = descricaoValidada,
            Valor = valor
        };
    }
}
