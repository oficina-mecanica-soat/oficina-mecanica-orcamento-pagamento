using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "OrcamentoPagamento");

            migrationBuilder.CreateTable(
                name: "Orcamentos",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Decisao = table.Column<int>(type: "int", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MensagemOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orcamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoNegocio = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Corpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublicadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PecasInsumosOrcamento",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    PecaInsumoCatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PecasInsumosOrcamento", x => new { x.OrcamentoId, x.PecaInsumoCatalogoId });
                    table.ForeignKey(
                        name: "FK_PecasInsumosOrcamento_Orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalSchema: "OrcamentoPagamento",
                        principalTable: "Orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServicosOrcamento",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    ServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServicoCatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicosOrcamento", x => new { x.OrcamentoId, x.ServicoId });
                    table.ForeignKey(
                        name: "FK_ServicosOrcamento_Orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalSchema: "OrcamentoPagamento",
                        principalTable: "Orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_OrdemServicoId_Versao",
                schema: "OrcamentoPagamento",
                table: "Orcamentos",
                columns: new[] { "OrdemServicoId", "Versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_PublicadoEm",
                schema: "OrcamentoPagamento",
                table: "Outbox",
                column: "PublicadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Tipo_OrdemServicoId_VersaoNegocio",
                schema: "OrcamentoPagamento",
                table: "Outbox",
                columns: new[] { "Tipo", "OrdemServicoId", "VersaoNegocio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "OrcamentoPagamento");

            migrationBuilder.DropTable(
                name: "PecasInsumosOrcamento",
                schema: "OrcamentoPagamento");

            migrationBuilder.DropTable(
                name: "ServicosOrcamento",
                schema: "OrcamentoPagamento");

            migrationBuilder.DropTable(
                name: "Orcamentos",
                schema: "OrcamentoPagamento");
        }
    }
}
