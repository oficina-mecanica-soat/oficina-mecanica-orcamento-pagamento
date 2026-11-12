using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;

public static class RegrasSnapshot
{
    public const int DescricaoMaxima = 160;
    public const decimal ValorMaximo = 9999999999999999.99m;

    public static void ValidarIdentificador(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(OrcamentoErrorMessages.IdentificadorObrigatorio);
        }
    }

    public static string ValidarDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao) || descricao.Trim().Length > DescricaoMaxima)
        {
            throw new DomainException(OrcamentoErrorMessages.DescricaoInvalida);
        }
        return descricao.Trim();
    }

    public static void ValidarValor(decimal valor)
    {
        if (valor <= 0 || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
        {
            throw new DomainException(OrcamentoErrorMessages.ValorInvalido);
        }
    }
}
