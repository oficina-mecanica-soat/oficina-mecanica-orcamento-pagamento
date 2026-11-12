namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;

[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class OrcamentoPagamentoApiCollection : ICollectionFixture<OrcamentoPagamentoApiFixture>
{
    public const string Nome = "OrcamentoPagamentoApi";
}
