using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;

public sealed class PecaInsumoOrcamento
{
    private PecaInsumoOrcamento() { Descricao = string.Empty; }
    public Guid PecaInsumoCatalogoId { get; private set; }
    public string Descricao { get; private set; }
    public int Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal ValorTotal => Quantidade * ValorUnitario;

    public static PecaInsumoOrcamento Criar(Guid catalogoId, string descricao, int quantidade, decimal valorUnitario)
    {
        RegrasSnapshot.ValidarIdentificador(catalogoId);
        var descricaoValidada = RegrasSnapshot.ValidarDescricao(descricao);
        if (quantidade <= 0)
        {
            throw new DomainException(OrcamentoErrorMessages.QuantidadeInvalida);
        }
        RegrasSnapshot.ValidarValor(valorUnitario);
        if (valorUnitario > RegrasSnapshot.ValorMaximo / quantidade)
        {
            throw new DomainException(OrcamentoErrorMessages.ValorInvalido);
        }
        return new PecaInsumoOrcamento
        {
            PecaInsumoCatalogoId = catalogoId,
            Descricao = descricaoValidada,
            Quantidade = quantidade,
            ValorUnitario = valorUnitario
        };
    }
}
