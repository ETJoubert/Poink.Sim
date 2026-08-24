using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poink.Sim.Data.Migrations.PoinkSim
{
	/// <inheritdoc />
	public partial class AddedNeuralWeightsMetadata : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<double>(
				name: "AvgReward",
				table: "NeuralWeights",
				type: "double precision",
				nullable: false,
				defaultValue: 0.0
			);

			migrationBuilder.AddColumn<string>(
				name: "Dataset",
				table: "NeuralWeights",
				type: "text",
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.AddColumn<string>(
				name: "ParametersJson",
				table: "NeuralWeights",
				type: "jsonb",
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.AddColumn<string>(
				name: "RewardPolicyName",
				table: "NeuralWeights",
				type: "text",
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.AddColumn<string>(
				name: "StrategyName",
				table: "NeuralWeights",
				type: "text",
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.AddColumn<string>(
				name: "StructureSnapshotJson",
				table: "NeuralWeights",
				type: "jsonb",
				nullable: true
			);

			migrationBuilder.AddColumn<DateTime>(
				name: "TrainedAt",
				table: "NeuralWeights",
				type: "timestamp with time zone",
				nullable: true
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "AvgReward", table: "NeuralWeights");

			migrationBuilder.DropColumn(name: "Dataset", table: "NeuralWeights");

			migrationBuilder.DropColumn(name: "ParametersJson", table: "NeuralWeights");

			migrationBuilder.DropColumn(name: "RewardPolicyName", table: "NeuralWeights");

			migrationBuilder.DropColumn(name: "StrategyName", table: "NeuralWeights");

			migrationBuilder.DropColumn(name: "StructureSnapshotJson", table: "NeuralWeights");

			migrationBuilder.DropColumn(name: "TrainedAt", table: "NeuralWeights");
		}
	}
}
