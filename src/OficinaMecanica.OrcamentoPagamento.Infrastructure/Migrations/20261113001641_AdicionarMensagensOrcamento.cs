using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMensagensOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseAte",
                schema: "OrcamentoPagamento",
                table: "Outbox",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProximaTentativaEm",
                schema: "OrcamentoPagamento",
                table: "Outbox",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Responsavel",
                schema: "OrcamentoPagamento",
                table: "Outbox",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tentativas",
                schema: "OrcamentoPagamento",
                table: "Outbox",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TraceContext",
                schema: "OrcamentoPagamento",
                table: "Orcamentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CancelamentosRecebidos",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelamentosRecebidos", x => new { x.OrdemServicoId, x.Versao });
                });

            migrationBuilder.CreateTable(
                name: "RecebimentosMensagem",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChaveOperacao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecebimentosMensagem", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosMensagem_ChaveOperacao",
                schema: "OrcamentoPagamento",
                table: "RecebimentosMensagem",
                column: "ChaveOperacao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CancelamentosRecebidos",
                schema: "OrcamentoPagamento");

            migrationBuilder.DropTable(
                name: "RecebimentosMensagem",
                schema: "OrcamentoPagamento");

            migrationBuilder.DropColumn(
                name: "LeaseAte",
                schema: "OrcamentoPagamento",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "ProximaTentativaEm",
                schema: "OrcamentoPagamento",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "Responsavel",
                schema: "OrcamentoPagamento",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "Tentativas",
                schema: "OrcamentoPagamento",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "TraceContext",
                schema: "OrcamentoPagamento",
                table: "Orcamentos");
        }
    }
}
