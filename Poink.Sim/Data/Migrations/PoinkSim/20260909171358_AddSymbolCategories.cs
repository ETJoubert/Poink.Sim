using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
    /// <inheritdoc />
    public partial class AddSymbolCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SymbolCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SymbolCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AssetClassId = table.Column<long>(type: "bigint", nullable: false),
                    AssetClassName = table.Column<string>(type: "text", nullable: false),
                    Bucket = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SymbolCategories_SymbolCategoryId",
                table: "SymbolCategories",
                column: "SymbolCategoryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SymbolCategories");
        }
    }
}
