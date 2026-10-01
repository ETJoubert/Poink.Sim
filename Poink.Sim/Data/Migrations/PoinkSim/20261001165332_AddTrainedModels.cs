using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
    /// <inheritdoc />
    public partial class AddTrainedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainedModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    Weights = table.Column<byte[]>(type: "bytea", nullable: false),
                    WeightCount = table.Column<long>(type: "bigint", nullable: false),
                    EpisodesTrained = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainedModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainedModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    EpisodeMetricsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EpisodesCompleted = table.Column<int>(type: "integer", nullable: false),
                    ModelEpisodesTrained = table.Column<int>(type: "integer", nullable: false),
                    RecentAverageReward = table.Column<double>(type: "double precision", nullable: false),
                    BestReward = table.Column<double>(type: "double precision", nullable: false),
                    WorstReward = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingHistoryEntries_TrainedModels_TrainedModelId",
                        column: x => x.TrainedModelId,
                        principalTable: "TrainedModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainedModels_Name",
                table: "TrainedModels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingHistoryEntries_TrainedModelId",
                table: "TrainingHistoryEntries",
                column: "TrainedModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingHistoryEntries");

            migrationBuilder.DropTable(
                name: "TrainedModels");
        }
    }
}
