using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
    /// <inheritdoc />
    public partial class AddSymbolMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SymbolMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SymbolId = table.Column<long>(type: "bigint", nullable: false),
                    Digits = table.Column<int>(type: "integer", nullable: false),
                    PipPosition = table.Column<int>(type: "integer", nullable: false),
                    EnableShortSelling = table.Column<bool>(type: "boolean", nullable: false),
                    GuaranteedStopLoss = table.Column<bool>(type: "boolean", nullable: false),
                    SwapRollover3Days = table.Column<int>(type: "integer", nullable: false),
                    SwapLong = table.Column<double>(type: "double precision", nullable: false),
                    SwapShort = table.Column<double>(type: "double precision", nullable: false),
                    MaxVolume = table.Column<long>(type: "bigint", nullable: false),
                    MinVolume = table.Column<long>(type: "bigint", nullable: false),
                    StepVolume = table.Column<long>(type: "bigint", nullable: false),
                    MaxExposure = table.Column<long>(type: "bigint", nullable: false),
                    CommissionType = table.Column<int>(type: "integer", nullable: false),
                    SlDistance = table.Column<int>(type: "integer", nullable: false),
                    TpDistance = table.Column<int>(type: "integer", nullable: false),
                    GslDistance = table.Column<int>(type: "integer", nullable: false),
                    GslCharge = table.Column<long>(type: "bigint", nullable: false),
                    DistanceSetIn = table.Column<int>(type: "integer", nullable: false),
                    MinCommissionType = table.Column<int>(type: "integer", nullable: false),
                    MinCommissionAsset = table.Column<string>(type: "text", nullable: false),
                    RolloverCommission = table.Column<long>(type: "bigint", nullable: false),
                    SkipRolloverDays = table.Column<int>(type: "integer", nullable: false),
                    ScheduleTimeZone = table.Column<string>(type: "text", nullable: false),
                    TradingMode = table.Column<int>(type: "integer", nullable: false),
                    RolloverCommission3Days = table.Column<int>(type: "integer", nullable: false),
                    SwapCalculationType = table.Column<int>(type: "integer", nullable: false),
                    LotSize = table.Column<long>(type: "bigint", nullable: false),
                    PreciseTradingCommissionRate = table.Column<long>(type: "bigint", nullable: false),
                    PreciseMinCommission = table.Column<long>(type: "bigint", nullable: false),
                    PnlConversionFeeRate = table.Column<int>(type: "integer", nullable: false),
                    LeverageId = table.Column<long>(type: "bigint", nullable: false),
                    SwapPeriod = table.Column<int>(type: "integer", nullable: false),
                    SwapTime = table.Column<int>(type: "integer", nullable: false),
                    SkipSWAPPeriods = table.Column<int>(type: "integer", nullable: false),
                    ChargeSwapAtWeekends = table.Column<bool>(type: "boolean", nullable: false),
                    MeasurementUnits = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SymbolHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SymbolMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    HolidayId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ScheduleTimeZone = table.Column<string>(type: "text", nullable: false),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    StartSecond = table.Column<int>(type: "integer", nullable: false),
                    EndSecond = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SymbolHolidays_SymbolMetadata_SymbolMetadataId",
                        column: x => x.SymbolMetadataId,
                        principalTable: "SymbolMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SymbolScheduleIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SymbolMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartSecond = table.Column<int>(type: "integer", nullable: false),
                    EndSecond = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolScheduleIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SymbolScheduleIntervals_SymbolMetadata_SymbolMetadataId",
                        column: x => x.SymbolMetadataId,
                        principalTable: "SymbolMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SymbolHolidays_SymbolMetadataId",
                table: "SymbolHolidays",
                column: "SymbolMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_SymbolMetadata_SymbolId",
                table: "SymbolMetadata",
                column: "SymbolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SymbolScheduleIntervals_SymbolMetadataId",
                table: "SymbolScheduleIntervals",
                column: "SymbolMetadataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SymbolHolidays");

            migrationBuilder.DropTable(
                name: "SymbolScheduleIntervals");

            migrationBuilder.DropTable(
                name: "SymbolMetadata");
        }
    }
}
