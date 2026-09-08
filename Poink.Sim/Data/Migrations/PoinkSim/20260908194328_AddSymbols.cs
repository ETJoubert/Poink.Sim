using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
    /// <inheritdoc />
    public partial class AddSymbols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Symbols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SymbolId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolName = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    BaseAssetId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteAssetId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SortingNumber = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Symbols", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Symbols_SymbolId",
                table: "Symbols",
                column: "SymbolId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Symbols");
        }
    }
}
