using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreservarLinhasPecaOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PecasInsumosOrcamento",
                schema: "OrcamentoPagamento",
                table: "PecasInsumosOrcamento");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "OrcamentoPagamento",
                table: "PecasInsumosOrcamento",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PecasInsumosOrcamento",
                schema: "OrcamentoPagamento",
                table: "PecasInsumosOrcamento",
                columns: new[] { "OrcamentoId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [OrcamentoPagamento].[PecasInsumosOrcamento]
                    GROUP BY [OrcamentoId], [PecaInsumoCatalogoId] HAVING COUNT(*) > 1
                )
                    THROW 50000, N'Reversão bloqueada: existem linhas de peças que a chave anterior não representa. Preserve os dados.', 1;
                """);
            migrationBuilder.DropPrimaryKey(
                name: "PK_PecasInsumosOrcamento",
                schema: "OrcamentoPagamento",
                table: "PecasInsumosOrcamento");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "OrcamentoPagamento",
                table: "PecasInsumosOrcamento");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PecasInsumosOrcamento",
                schema: "OrcamentoPagamento",
                table: "PecasInsumosOrcamento",
                columns: new[] { "OrcamentoId", "PecaInsumoCatalogoId" });
        }
    }
}
