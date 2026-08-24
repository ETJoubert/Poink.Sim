using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Poink.Sim.Repository;

internal sealed class TRangeChartRepository : ITRangeChartRepository
{
	const string DatabaseName = "Poink.Sim";
	const string TableName = "trange_charts";

	private readonly ILogger<TRangeChartRepository> _logger;
	private readonly string _maintenanceConnectionString;
	private readonly string _connectionString;

	public TRangeChartRepository(ILogger<TRangeChartRepository> logger, IConfiguration configuration)
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

	public long SaveTRangeChart(
		string label,
		string symbol,
		System.DateTime startTimeUtc,
		System.DateTime endTimeUtc,
		double barMinutes,
		int barCount
	)
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTable(conn);

		long id = conn.ExecuteScalar<long>(
			Sql.InsertChart,
			new
			{
				label,
				symbol,
				startTimeUtc,
				endTimeUtc,
				barMinutes,
				barCount,
			}
		);
		_logger.LogInformation(
			"Saved T-Range Chart on {Symbol} [{Start} - {End}] as id {Id} labeled {Label}",
			symbol,
			startTimeUtc,
			endTimeUtc,
			id,
			label
		);
		return id;
	}

	public List<TRangeChart> ListTRangeCharts()
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTable(conn);

		var result = conn.Query<TRangeChart>(Sql.SelectCharts).ToList();
		_logger.LogInformation("Listed {ChartCount} saved T-Range Charts", result.Count);
		return result;
	}

	public void DeleteTRangeChart(long id)
	{
		EnsureDatabaseExists();
		using var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		EnsureTable(conn);
		conn.Execute(Sql.DeleteChart, new { id });
		_logger.LogInformation("Deleted saved T-Range Chart id {Id}", id);
	}

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

	private static void EnsureTable(NpgsqlConnection conn) => conn.Execute(Sql.CreateTable);

	// ── SQL ──
	private static class Sql
	{
		public const string DatabaseExists = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";

		public const string CreateDatabase = $"CREATE DATABASE \"{DatabaseName}\"";

		public const string CreateTable =
			$@"CREATE TABLE IF NOT EXISTS ""{TableName}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""label"" TEXT NOT NULL,
                   ""symbol"" TEXT NOT NULL,
                   ""start_time_utc"" TIMESTAMPTZ NOT NULL,
                   ""end_time_utc"" TIMESTAMPTZ NOT NULL,
                   ""bar_minutes"" DOUBLE PRECISION NOT NULL,
                   ""bar_count"" INTEGER NOT NULL,
                   ""saved_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );";

		public const string InsertChart =
			$@"INSERT INTO ""{TableName}"" (""label"", ""symbol"", ""start_time_utc"", ""end_time_utc"", ""bar_minutes"", ""bar_count"", ""saved_at"")
               VALUES (@label, @symbol, @startTimeUtc, @endTimeUtc, @barMinutes, @barCount, now())
               RETURNING ""id""";

		public const string SelectCharts =
			$@"SELECT ""id"" AS ""Id"", ""label"" AS ""Label"", ""symbol"" AS ""Symbol"", ""start_time_utc"" AS ""StartTimeUtc"",
                      ""end_time_utc"" AS ""EndTimeUtc"", ""bar_minutes"" AS ""BarMinutes"", ""bar_count"" AS ""BarCount"", ""saved_at"" AS ""SavedAt""
               FROM ""{TableName}"" ORDER BY ""saved_at"" DESC";

		public const string DeleteChart = $@"DELETE FROM ""{TableName}"" WHERE ""id"" = @id";
	}
}
