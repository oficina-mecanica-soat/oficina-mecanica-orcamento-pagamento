using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pagamentos",
                schema: "OrcamentoPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckoutId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EnderecoCheckout = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IdentificadorPagamentoProvedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MensagemOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraceContext = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalSchema: "OrcamentoPagamento",
                        principalTable: "Orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_OrcamentoId",
                schema: "OrcamentoPagamento",
                table: "Pagamentos",
                column: "OrcamentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_OrdemServicoId_Versao",
                schema: "OrcamentoPagamento",
                table: "Pagamentos",
                columns: new[] { "OrdemServicoId", "Versao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pagamentos",
                schema: "OrcamentoPagamento");
        }
    }
}
