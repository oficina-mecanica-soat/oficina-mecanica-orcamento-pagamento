using System.Text.Json;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Factories;

internal static class MensagemOrcamentoTestDataFactory
{
    public static MensagemIntegracao Gerar() => Criar("GerarOrcamento", new GerarOrcamentoDados(1, Guid.NewGuid(),
        [new(Guid.NewGuid(), Guid.NewGuid(), "Troca de óleo", 150m)], [new(Guid.NewGuid(), "Óleo", 2, 45m)]));
    public static MensagemIntegracao Criar(string tipo, object dados, Guid? sagaId = null, Guid? ordemServicoId = null) =>
        new(Guid.NewGuid(), tipo, 1, sagaId ?? Guid.NewGuid(), ordemServicoId ?? Guid.NewGuid(), "correlacao-integracao", Guid.NewGuid(),
            DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(dados, JsonContrato.Options));
}
