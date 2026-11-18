using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Builders;

internal static class OrcamentoRequestBuilder
{
    public static GerarOrcamentoRequest Novo(Guid? clienteId = null) =>
        new(Guid.NewGuid(), clienteId ?? Guid.NewGuid(), 1,
            new ContextoIntegracao(Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid()),
            [new(Guid.NewGuid(), Guid.NewGuid(), "Troca de óleo", 150m)],
            [new(Guid.NewGuid(), "Óleo", 2, 45m)]);
}
