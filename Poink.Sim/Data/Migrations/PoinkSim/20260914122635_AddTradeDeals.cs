using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
    /// <inheritdoc />
    public partial class AddTradeDeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    PositionId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolName = table.Column<string>(type: "text", nullable: false),
                    IsLong = table.Column<bool>(type: "boolean", nullable: false),
                    Volume = table.Column<double>(type: "double precision", nullable: false),
                    ExecutionPrice = table.Column<double>(type: "double precision", nullable: false),
                    ExecutionTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RealizedPnl = table.Column<double>(type: "double precision", nullable: true),
                    Commission = table.Column<double>(type: "double precision", nullable: true),
                    Swap = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeDeals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeDeals_AccountId_ExecutionTimeUtc",
                table: "TradeDeals",
                columns: new[] { "AccountId", "ExecutionTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeDeals_DealId",
                table: "TradeDeals",
                column: "DealId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeDeals");
        }
    }
}
