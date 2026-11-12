using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;

namespace OficinaMecanica.OrcamentoPagamento.Domain.UnitTests.Orcamentos.Factories;

internal static class OrcamentoTestDataFactory
{
    public const decimal ValorServico = 150m;
    public const decimal ValorPeca = 45m;
    public static Orcamento Criar(int versao = 1, Guid? ordemId = null, Guid? clienteId = null,
        Guid? servicoId = null, Guid? catalogoId = null, Guid? pecaId = null)
    {
        return Orcamento.Gerar(ordemId ?? Guid.NewGuid(), clienteId ?? Guid.NewGuid(), versao,
            [ServicoOrcamento.Criar(servicoId ?? Guid.NewGuid(), catalogoId ?? Guid.NewGuid(), "Troca de óleo", ValorServico)],
            [PecaInsumoOrcamento.Criar(pecaId ?? Guid.NewGuid(), "Óleo", 2, ValorPeca)]);
    }
}
