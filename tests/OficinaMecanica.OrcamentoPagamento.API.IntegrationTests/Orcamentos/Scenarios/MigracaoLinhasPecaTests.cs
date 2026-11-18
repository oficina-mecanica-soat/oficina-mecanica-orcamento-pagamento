using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

[Collection(OrcamentoPagamentoApiCollection.Nome)]
public sealed class MigracaoLinhasPecaTests(OrcamentoPagamentoApiFixture fixture)
{
    [Fact]
    public async Task Dado_BancoComLinhaAntiga_Quando_AplicarMigration_Entao_DevePreservarPrecoQuantidadeEReferencia()
    {
        // Arrange: banco descartável próprio, sem alterar o banco da fixture ou ambientes reais.
        var conexao = new SqlConnectionStringBuilder(fixture.ConnectionString)
        { InitialCatalog = "OrcamentoMigracao_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using var contexto = new OrcamentoPagamentoDbContext(
            new DbContextOptionsBuilder<OrcamentoPagamentoDbContext>().UseSqlServer(conexao).Options);
        try
        {
            var migrator = contexto.GetService<IMigrator>();
            var anterior = contexto.Database.GetMigrations().Last(m => !m.EndsWith("PreservarLinhasPecaOrcamento"));
            await migrator.MigrateAsync(anterior);
            var id = Guid.NewGuid();
            var ordemId = Guid.NewGuid();
            var clienteId = Guid.NewGuid();
            var sagaId = Guid.NewGuid();
            var mensagemId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var servicoId = Guid.NewGuid();
            var catalogoId = Guid.NewGuid();
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [OrcamentoPagamento].[Orcamentos]
                    ([Id],[OrdemServicoId],[ClienteId],[Versao],[Status],[ValorTotal],[CorrelationId],[MensagemOrigemId],[SagaId])
                    VALUES ({id},{ordemId},{clienteId},1,1,240.40,N'migracao-local',{mensagemId},{sagaId});
                INSERT INTO [OrcamentoPagamento].[PecasInsumosOrcamento]
                    ([OrcamentoId],[PecaInsumoCatalogoId],[Descricao],[Quantidade],[ValorUnitario])
                    VALUES ({id},{itemId},N'Peça preservada',2,45.20);
                INSERT INTO [OrcamentoPagamento].[ServicosOrcamento]
                    ([OrcamentoId],[ServicoId],[ServicoCatalogoId],[Descricao],[Valor])
                    VALUES ({id},{servicoId},{catalogoId},N'Serviço preservado',150);
                """);
            // Act
            await migrator.MigrateAsync();
            // Assert
            var orcamento = await contexto.Orcamentos.Include(o => o.PecasInsumos).SingleAsync();
            orcamento.ValorTotal.Should().Be(240.40m);
            var peca = orcamento.PecasInsumos.Should().ContainSingle().Which;
            peca.PecaInsumoCatalogoId.Should().Be(itemId);
            peca.Quantidade.Should().Be(2);
            peca.ValorUnitario.Should().Be(45.20m);
        }
        finally
        {
            await contexto.Database.EnsureDeletedAsync();
        }
    }
}
