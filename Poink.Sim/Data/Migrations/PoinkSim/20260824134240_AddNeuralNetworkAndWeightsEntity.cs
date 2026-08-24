using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
    /// <inheritdoc />
    public partial class AddNeuralNetworkAndWeightsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NeuralNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StructureJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeuralNetworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NeuralWeights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NeuralNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    WeightCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeuralWeights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeuralWeights_NeuralNetworks_NeuralNetworkId",
                        column: x => x.NeuralNetworkId,
                        principalTable: "NeuralNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NeuralWeights_NeuralNetworkId",
                table: "NeuralWeights",
                column: "NeuralNetworkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NeuralWeights");

            migrationBuilder.DropTable(
                name: "NeuralNetworks");
        }
    }
}
