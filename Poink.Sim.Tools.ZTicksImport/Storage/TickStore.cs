using System;
using System.Collections.Generic;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Poink.ZTicks;

namespace Poink.Sim.Tools.ZTicksImport.Storage;

/// <summary>Mirrors Poink.Sim.Services.MarketData.TickIngest's DDL/DML so imported history is indistinguishable from live-ingested ticks.</summary>
internal sealed class TickStore
{
	private const string RegistryTable = "tick_table_stats";
	private const string BackfillLogTable = "tick_backfill_log";

	private readonly string _connectionString;
	private readonly ILogger<TickStore> _logger;

	public TickStore(string connectionString, ILogger<TickStore> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
	}

	public void EnsureTable(string table)
	{
		using var connection = new NpgsqlConnection(_connectionString);
		connection.Open();
		EnsureDdl(connection, Sql.CreateTickTable(table));
		EnsureDdl(connection, Sql.CreateRegistryTable);
		connection.Execute(Sql.EnsureRegistryRow, new { table });
		_logger.LogInformation("Ensured tick table {Table}.", table);
	}

	public void AssociateSymbol(string table, string symbolName)
	{
		using var connection = new NpgsqlConnection(_connectionString);
		connection.Open();
		connection.Execute(Sql.UpdateSymbol, new { table, symbol = symbolName });
		_logger.LogInformation("Associated symbol {Symbol} with table {Table}.", symbolName, table);
	}

	public long InsertTicks(string table, IReadOnlyList<TickRecord> ticks)
	{
		if (ticks.Count == 0)
			return 0;

		using var connection = new NpgsqlConnection(_connectionString);
		connection.Open();

		var staging = $"stage_{Guid.NewGuid():N}";
		long written;
		try
		{
			connection.Execute(Sql.CreateStagingTable(staging));

			using (var writer = connection.BeginBinaryImport(Sql.CopyInto(staging)))
			{
				foreach (var tick in ticks)
				{
					writer.StartRow();
					writer.Write(DateTime.SpecifyKind(tick.TimestampUtc, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
					writer.Write(tick.Bid, NpgsqlDbType.Double);
					writer.Write(tick.Ask, NpgsqlDbType.Double);
				}
				writer.Complete();
			}

			written = connection.Execute(Sql.InsertFromStagingSkipDuplicates(table, staging));
		}
		finally
		{
			connection.Execute(Sql.DropStagingTable(staging));
		}

		connection.Execute(
			Sql.UpsertRegistryDelta,
			new
			{
				table,
				n = written,
				min = ticks[0].TimestampUtc,
				max = ticks[^1].TimestampUtc,
			}
		);

		return written;
	}

	public void LogBackfill(string table, string symbolName, DateTime fromUtc, DateTime toUtc, long recordCount)
	{
		using var connection = new NpgsqlConnection(_connectionString);
		connection.Open();
		EnsureDdl(connection, Sql.CreateBackfillLogTable);
		connection.Execute(
			Sql.InsertBackfillLog,
			new
			{
				table,
				symbolName,
				from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
				to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
				recordCount,
			}
		);
	}

	/// <summary>Runs idempotent DDL, tolerating the race where a concurrent caller wins CREATE ... IF NOT EXISTS first.</summary>
	private static void EnsureDdl(NpgsqlConnection connection, string ddl)
	{
		try
		{
			connection.Execute(ddl);
		}
		catch (PostgresException ex) when (ex.SqlState is "42P07" or "42710") { }
	}

	private static class Sql
	{
		public static string CreateTickTable(string table) =>
			$@"CREATE EXTENSION IF NOT EXISTS timescaledb;
               CREATE TABLE IF NOT EXISTS ""{table}"" (
                   ""timestamp"" TIMESTAMPTZ NOT NULL UNIQUE,
                   ""bid"" DOUBLE PRECISION NOT NULL,
                   ""ask"" DOUBLE PRECISION NOT NULL
               );
               SELECT create_hypertable('{table}', 'timestamp', if_not_exists => true);";

		public static string CopyInto(string table) => $@"COPY ""{table}"" (""timestamp"", ""bid"", ""ask"") FROM STDIN (FORMAT BINARY)";

		public static string CreateStagingTable(string staging) =>
			$@"CREATE TEMP TABLE ""{staging}"" (
                   ""timestamp"" TIMESTAMPTZ NOT NULL,
                   ""bid"" DOUBLE PRECISION NOT NULL,
                   ""ask"" DOUBLE PRECISION NOT NULL
               );";

		public static string InsertFromStagingSkipDuplicates(string table, string staging) =>
			$@"INSERT INTO ""{table}"" (""timestamp"", ""bid"", ""ask"")
               SELECT ""timestamp"", ""bid"", ""ask"" FROM ""{staging}""
               ON CONFLICT (""timestamp"") DO NOTHING";

		public static string DropStagingTable(string staging) => $@"DROP TABLE IF EXISTS ""{staging}""";

		public const string CreateRegistryTable =
			$@"CREATE TABLE IF NOT EXISTS ""{RegistryTable}"" (
                   ""table_name"" TEXT PRIMARY KEY,
                   ""row_count"" BIGINT NOT NULL,
                   ""min_timestamp"" TIMESTAMPTZ,
                   ""max_timestamp"" TIMESTAMPTZ,
                   ""symbol"" TEXT,
                   ""refreshed_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );
               ALTER TABLE ""{RegistryTable}"" ADD COLUMN IF NOT EXISTS ""symbol"" TEXT;";

		public const string EnsureRegistryRow =
			$@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""refreshed_at"")
               VALUES (@table, 0, now())
               ON CONFLICT (""table_name"") DO NOTHING";

		public const string UpdateSymbol =
			$@"UPDATE ""{RegistryTable}"" SET ""symbol"" = @symbol, ""refreshed_at"" = now() WHERE ""table_name"" = @table";

		public const string UpsertRegistryDelta =
			$@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""min_timestamp"", ""max_timestamp"", ""refreshed_at"")
               VALUES (@table, @n, @min, @max, now())
               ON CONFLICT (""table_name"") DO UPDATE SET
                   ""row_count"" = ""{RegistryTable}"".""row_count"" + @n,
                   ""min_timestamp"" = LEAST(COALESCE(""{RegistryTable}"".""min_timestamp"", EXCLUDED.""min_timestamp""), EXCLUDED.""min_timestamp""),
                   ""max_timestamp"" = GREATEST(COALESCE(""{RegistryTable}"".""max_timestamp"", EXCLUDED.""max_timestamp""), EXCLUDED.""max_timestamp""),
                   ""refreshed_at"" = now()";

		public const string CreateBackfillLogTable =
			$@"CREATE TABLE IF NOT EXISTS ""{BackfillLogTable}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""tick_table_name"" TEXT NOT NULL,
                   ""symbol_code"" BIGINT NOT NULL,
                   ""symbol_name"" TEXT NOT NULL,
                   ""backfill_from"" TIMESTAMPTZ NOT NULL,
                   ""backfill_to"" TIMESTAMPTZ NOT NULL,
                   ""record_count"" BIGINT NOT NULL,
                   ""trigger_entity"" TEXT NOT NULL CHECK (""trigger_entity"" IN ('User', 'Worker')),
                   ""triggered_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );
               CREATE INDEX IF NOT EXISTS ""ix_{BackfillLogTable}_table"" ON ""{BackfillLogTable}"" (""tick_table_name"");";

		public static string InsertBackfillLog =>
			$@"INSERT INTO ""{BackfillLogTable}""
                   (""tick_table_name"", ""symbol_code"", ""symbol_name"", ""backfill_from"", ""backfill_to"", ""record_count"", ""trigger_entity"")
               VALUES (@table, 0, @symbolName, @from, @to, @recordCount, 'User')";
	}
}
