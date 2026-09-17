using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

// Two tables: "detail" (one row per saved run, scalar stats shared by both analysis types via
// nullable columns) and "bins" (one row per histogram bucket, keyed by run and optional series).
internal sealed class AnalysisHistoryRepository : IAnalysisHistoryRepository
{
	const string DatabaseName = "Poink.Sim";
	const string DetailTableName = "analysis_history";
	const string BinsTableName = "analysis_history_bins";

	private readonly ILogger<AnalysisHistoryRepository> _logger;
	private readonly string _maintenanceConnectionString;
	private readonly string _connectionString;

	public AnalysisHistoryRepository(ILogger<AnalysisHistoryRepository> logger, IConfiguration configuration)
	{
		_logger = logger;
		_maintenanceConnectionString =
			configuration.GetConnectionString("PoinkSimMaintenance")
			?? throw new InvalidOperationException(
				"No connection string configured for 'PoinkSimMaintenance'. Add ConnectionStrings:PoinkSimMaintenance "
					+ "to appsettings, appsettings.Secrets.json, or environment variables."
			);
		_connectionString =
			configuration.GetConnectionString("PoinkSim")
			?? throw new InvalidOperationException(
				"No connection string configured for 'PoinkSim'. Add ConnectionStrings:PoinkSim "
					+ "to appsettings, appsettings.Secrets.json, or environment variables."
			);
	}

	public long SaveTimeAboveBelow(string label, TimeAboveBelowRequest request, TimeAboveBelowDistributionResult result)
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTables(conn);
		using var tx = conn.BeginTransaction();

		var id = conn.ExecuteScalar<long>(
			Sql.InsertDetail,
			new
			{
				analysisType = "TimeAboveBelow",
				label,
				tableName = request.Table,
				fromTimeUtc = request.From,
				toTimeUtc = request.To,
				windowMinutes = (double?)request.WindowMinutes,
				aggregationBarWidthSeconds = (int?)null,
				aggregationWindowBars = (int?)null,
				aggregationKind = (string?)null,
				sampleCount = result.SampleCount,
				returnedCount = (long?)null,
				returnProbability = (double?)null,
				meanToReturn = (double?)null,
				medianToReturn = (double?)null,
				toReturnUnit = (string?)null,
				meanFractionAbove = (double?)result.MeanFractionAbove,
				medianFractionAbove = (double?)result.MedianFractionAbove,
				stdDevFractionAbove = (double?)result.StdDevFractionAbove,
				minFractionAbove = (double?)result.MinFractionAbove,
				maxFractionAbove = (double?)result.MaxFractionAbove,
				seriesBreakdownJson = (string?)null,
			},
			tx
		);
		InsertBins(conn, tx, id, series: null, result.Bins);

		tx.Commit();
		_logger.LogInformation("Saved Time Above/Below analysis on {Table} as history id {Id} labeled {Label}", request.Table, id, label);
		return id;
	}

	public long SaveReturnProbability(string label, PriceReturnRequest request, PriceReturnDistributionResult result)
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTables(conn);
		using var tx = conn.BeginTransaction();

		var seriesBreakdownJson =
			result.SeriesBreakdown is { } breakdown
				? JsonSerializer.Serialize(
					breakdown.Select(s => new AnalysisHistorySeriesSummary(s.Series, s.ReturnedCount, s.ReturnProbability, s.MeanToReturn, s.MedianToReturn))
				)
				: null;

		var id = conn.ExecuteScalar<long>(
			Sql.InsertDetail,
			new
			{
				analysisType = "ReturnProbability",
				label,
				tableName = request.Table,
				fromTimeUtc = request.From,
				toTimeUtc = request.To,
				windowMinutes = request.AggregationBarWidthSeconds is null ? (double?)request.WindowMinutes : null,
				aggregationBarWidthSeconds = request.AggregationBarWidthSeconds,
				aggregationWindowBars = request.AggregationWindowBars,
				aggregationKind = request.AggregationBarWidthSeconds is null ? null : request.AggregationKind.ToString(),
				sampleCount = result.SampleCount,
				returnedCount = (long?)result.ReturnedCount,
				returnProbability = (double?)result.ReturnProbability,
				meanToReturn = (double?)result.MeanToReturn,
				medianToReturn = (double?)result.MedianToReturn,
				toReturnUnit = result.ToReturnUnit,
				meanFractionAbove = (double?)null,
				medianFractionAbove = (double?)null,
				stdDevFractionAbove = (double?)null,
				minFractionAbove = (double?)null,
				maxFractionAbove = (double?)null,
				seriesBreakdownJson,
			},
			tx
		);
		InsertBins(conn, tx, id, series: null, result.ToReturnBins);
		if (result.SeriesBreakdown is { } breakdownForBins)
			foreach (var series in breakdownForBins)
				InsertBins(conn, tx, id, series.Series, series.ToReturnBins);

		tx.Commit();
		_logger.LogInformation("Saved Return Probability analysis on {Table} as history id {Id} labeled {Label}", request.Table, id, label);
		return id;
	}

	public List<AnalysisHistoryRecord> ListHistory()
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTables(conn);

		var rows = conn.Query<DetailRow>(Sql.SelectDetails).ToList();
		var result = rows.Select(ToRecord).ToList();
		_logger.LogInformation("Listed {HistoryCount} saved analyses", result.Count);
		return result;
	}

	public AnalysisHistoryDetail GetDetail(long id)
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTables(conn);

		var row = conn.QuerySingle<DetailRow>(Sql.SelectDetailById, new { id });
		var bins = conn.Query<AnalysisHistoryBin>(Sql.SelectBinsByRunId, new { runId = id }).ToList();
		return new AnalysisHistoryDetail(ToRecord(row), bins);
	}

	public void DeleteHistory(long id)
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTables(conn);
		conn.Execute(Sql.DeleteDetail, new { id });
		_logger.LogInformation("Deleted saved analysis id {Id}", id);
	}

	private static void InsertBins(NpgsqlConnection conn, NpgsqlTransaction tx, long runId, string? series, IReadOnlyList<HistogramBin> bins)
	{
		for (var i = 0; i < bins.Count; i++)
			conn.Execute(Sql.InsertBin, new { runId, series, binIndex = i, low = bins[i].Low, high = bins[i].High, count = bins[i].Count }, tx);
	}

	private static AnalysisHistoryRecord ToRecord(DetailRow row) =>
		new(
			row.Id,
			row.AnalysisType,
			row.Label,
			row.TableName,
			row.FromTimeUtc,
			row.ToTimeUtc,
			row.WindowMinutes,
			row.AggregationBarWidthSeconds,
			row.AggregationWindowBars,
			row.AggregationKind,
			row.SampleCount,
			row.ReturnedCount,
			row.ReturnProbability,
			row.MeanToReturn,
			row.MedianToReturn,
			row.ToReturnUnit,
			row.MeanFractionAbove,
			row.MedianFractionAbove,
			row.StdDevFractionAbove,
			row.MinFractionAbove,
			row.MaxFractionAbove,
			row.SeriesBreakdownJson is null ? null : JsonSerializer.Deserialize<List<AnalysisHistorySeriesSummary>>(row.SeriesBreakdownJson),
			row.SavedAtUtc
		);

	private void EnsureDatabaseExists()
	{
		using var conn = new NpgsqlConnection(_maintenanceConnectionString);
		conn.Open();
		bool exists = conn.ExecuteScalar<bool>(Sql.DatabaseExists, new { name = DatabaseName });
		if (!exists)
		{
			_logger.LogInformation("Database {DatabaseName} does not exist; creating it", DatabaseName);
			conn.Execute(Sql.CreateDatabase);
		}
	}

	private static void EnsureTables(NpgsqlConnection conn)
	{
		conn.Execute(Sql.CreateDetailTable);
		conn.Execute(Sql.CreateBinsTable);
		conn.Execute(Sql.CreateBinsRunIdIndex);
	}

	// Maps column-by-column onto AnalysisHistoryRecord; kept separate since the JSONB column needs
	// to round-trip through a plain string before AnalysisHistorySeriesSummary deserialization.
	private sealed record DetailRow(
		long Id,
		string AnalysisType,
		string Label,
		string TableName,
		DateTime FromTimeUtc,
		DateTime ToTimeUtc,
		double? WindowMinutes,
		int? AggregationBarWidthSeconds,
		int? AggregationWindowBars,
		string? AggregationKind,
		long SampleCount,
		long? ReturnedCount,
		double? ReturnProbability,
		double? MeanToReturn,
		double? MedianToReturn,
		string? ToReturnUnit,
		double? MeanFractionAbove,
		double? MedianFractionAbove,
		double? StdDevFractionAbove,
		double? MinFractionAbove,
		double? MaxFractionAbove,
		string? SeriesBreakdownJson,
		DateTime SavedAtUtc
	);

	// ── SQL ──
	private static class Sql
	{
		public const string DatabaseExists = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";

		public const string CreateDatabase = $"CREATE DATABASE \"{DatabaseName}\"";

		public const string CreateDetailTable =
			$@"CREATE TABLE IF NOT EXISTS ""{DetailTableName}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""analysis_type"" TEXT NOT NULL,
                   ""label"" TEXT NOT NULL,
                   ""table_name"" TEXT NOT NULL,
                   ""from_time_utc"" TIMESTAMPTZ NOT NULL,
                   ""to_time_utc"" TIMESTAMPTZ NOT NULL,
                   ""window_minutes"" DOUBLE PRECISION NULL,
                   ""aggregation_bar_width_seconds"" INTEGER NULL,
                   ""aggregation_window_bars"" INTEGER NULL,
                   ""aggregation_kind"" TEXT NULL,
                   ""sample_count"" BIGINT NOT NULL,
                   ""returned_count"" BIGINT NULL,
                   ""return_probability"" DOUBLE PRECISION NULL,
                   ""mean_to_return"" DOUBLE PRECISION NULL,
                   ""median_to_return"" DOUBLE PRECISION NULL,
                   ""to_return_unit"" TEXT NULL,
                   ""mean_fraction_above"" DOUBLE PRECISION NULL,
                   ""median_fraction_above"" DOUBLE PRECISION NULL,
                   ""stddev_fraction_above"" DOUBLE PRECISION NULL,
                   ""min_fraction_above"" DOUBLE PRECISION NULL,
                   ""max_fraction_above"" DOUBLE PRECISION NULL,
                   ""series_breakdown"" JSONB NULL,
                   ""saved_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );";

		public const string CreateBinsTable =
			$@"CREATE TABLE IF NOT EXISTS ""{BinsTableName}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""run_id"" BIGINT NOT NULL REFERENCES ""{DetailTableName}""(""id"") ON DELETE CASCADE,
                   ""series"" TEXT NULL,
                   ""bin_index"" INTEGER NOT NULL,
                   ""low"" DOUBLE PRECISION NOT NULL,
                   ""high"" DOUBLE PRECISION NOT NULL,
                   ""count"" BIGINT NOT NULL
               );";

		public const string CreateBinsRunIdIndex = $@"CREATE INDEX IF NOT EXISTS ""idx_{BinsTableName}_run_id"" ON ""{BinsTableName}"" (""run_id"")";

		public const string InsertDetail =
			$@"INSERT INTO ""{DetailTableName}"" (
                   ""analysis_type"", ""label"", ""table_name"", ""from_time_utc"", ""to_time_utc"",
                   ""window_minutes"", ""aggregation_bar_width_seconds"", ""aggregation_window_bars"", ""aggregation_kind"",
                   ""sample_count"", ""returned_count"", ""return_probability"", ""mean_to_return"", ""median_to_return"", ""to_return_unit"",
                   ""mean_fraction_above"", ""median_fraction_above"", ""stddev_fraction_above"", ""min_fraction_above"", ""max_fraction_above"",
                   ""series_breakdown"", ""saved_at""
               ) VALUES (
                   @analysisType, @label, @tableName, @fromTimeUtc, @toTimeUtc,
                   @windowMinutes, @aggregationBarWidthSeconds, @aggregationWindowBars, @aggregationKind,
                   @sampleCount, @returnedCount, @returnProbability, @meanToReturn, @medianToReturn, @toReturnUnit,
                   @meanFractionAbove, @medianFractionAbove, @stdDevFractionAbove, @minFractionAbove, @maxFractionAbove,
                   @seriesBreakdownJson::jsonb, now()
               ) RETURNING ""id""";

		public const string InsertBin =
			$@"INSERT INTO ""{BinsTableName}"" (""run_id"", ""series"", ""bin_index"", ""low"", ""high"", ""count"")
               VALUES (@runId, @series, @binIndex, @low, @high, @count)";

		private const string DetailColumns =
			$@"""id"" AS ""Id"", ""analysis_type"" AS ""AnalysisType"", ""label"" AS ""Label"", ""table_name"" AS ""TableName"",
               ""from_time_utc"" AS ""FromTimeUtc"", ""to_time_utc"" AS ""ToTimeUtc"", ""window_minutes"" AS ""WindowMinutes"",
               ""aggregation_bar_width_seconds"" AS ""AggregationBarWidthSeconds"", ""aggregation_window_bars"" AS ""AggregationWindowBars"",
               ""aggregation_kind"" AS ""AggregationKind"", ""sample_count"" AS ""SampleCount"", ""returned_count"" AS ""ReturnedCount"",
               ""return_probability"" AS ""ReturnProbability"", ""mean_to_return"" AS ""MeanToReturn"", ""median_to_return"" AS ""MedianToReturn"",
               ""to_return_unit"" AS ""ToReturnUnit"", ""mean_fraction_above"" AS ""MeanFractionAbove"", ""median_fraction_above"" AS ""MedianFractionAbove"",
               ""stddev_fraction_above"" AS ""StdDevFractionAbove"", ""min_fraction_above"" AS ""MinFractionAbove"", ""max_fraction_above"" AS ""MaxFractionAbove"",
               ""series_breakdown""::text AS ""SeriesBreakdownJson"", ""saved_at"" AS ""SavedAtUtc""";

		public const string SelectDetails = $@"SELECT {DetailColumns} FROM ""{DetailTableName}"" ORDER BY ""saved_at"" DESC";

		public const string SelectDetailById = $@"SELECT {DetailColumns} FROM ""{DetailTableName}"" WHERE ""id"" = @id";

		public const string SelectBinsByRunId =
			$@"SELECT ""series"" AS ""Series"", ""bin_index"" AS ""BinIndex"", ""low"" AS ""Low"", ""high"" AS ""High"", ""count"" AS ""Count""
               FROM ""{BinsTableName}"" WHERE ""run_id"" = @runId ORDER BY ""series"" NULLS FIRST, ""bin_index""";

		public const string DeleteDetail = $@"DELETE FROM ""{DetailTableName}"" WHERE ""id"" = @id";
	}
}
