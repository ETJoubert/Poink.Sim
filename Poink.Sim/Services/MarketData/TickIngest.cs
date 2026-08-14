using System;
using System.Collections.Generic;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData
{
    internal sealed class TickIngest : ITickIngest
    {
        private const string RegistryTable = "tick_table_stats";
        private const string BackfillLogTable = "tick_backfill_log";

        private readonly string _connectionString;
        private readonly ILogger<TickIngest> _logger;

        public TickIngest(IConfiguration configuration, ILogger<TickIngest> logger)
        {
            _connectionString = configuration.GetConnectionString("PoinkMarketData")
                ?? throw new InvalidOperationException(
                    "No connection string configured for 'PoinkMarketData'. Add ConnectionStrings:PoinkMarketData " +
                    "to appsettings, appsettings.Secrets.json, or environment variables.");
            _logger = logger;
        }

        public bool TableExists(string table)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            return conn.QuerySingle<bool>(Sql.TableExists, new { table });
        }

        public string? GetTableForSymbol(string symbolCode)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            EnsureRegistryTable(conn);
            return conn.QuerySingleOrDefault<string>(Sql.SelectTableForSymbol, new { symbol = symbolCode });
        }

        public void CreateTable(string table)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            EnsureTable(conn, Sql.CreateTickTable(table));
            EnsureRegistryTable(conn);
            conn.Execute(Sql.EnsureRegistryRow, new { table });
            _logger.LogInformation("Created tick table {Table}.", table);
        }

        public void AssociateSymbol(string table, string symbolCode)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            EnsureRegistryTable(conn);

            if (conn.QuerySingleOrDefault<RegistryRow>(Sql.SelectRegistryRow, new { table }) is not null)
            {
                conn.Execute(Sql.UpdateSymbol, new { table, symbol = symbolCode });
            }
            else
            {
                var stats = conn.QuerySingle<StatsRow>(Sql.SelectTableStats(table));
                conn.Execute(Sql.InsertRowWithSymbol,
                    new { table, rows = stats.Rows, min = stats.MinTime, max = stats.MaxTime, symbol = symbolCode });
            }

            _logger.LogInformation("Associated symbol {Symbol} with table {Table}.", symbolCode, table);
        }

        public TickCoverage GetCoverage(string table)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            if (!conn.QuerySingle<bool>(Sql.TableExists, new { table }))
                return new TickCoverage(false, 0, null, null);

            EnsureRegistryTable(conn);
            var row = conn.QuerySingleOrDefault<RegistryRow>(Sql.SelectRegistryRow, new { table });

            if (row is not null)
                return new TickCoverage(true, row.RowCount, row.MinTimestamp, row.MaxTimestamp);

            var stats = conn.QuerySingle<StatsRow>(Sql.SelectTableStats(table));
            conn.Execute(Sql.UpsertRegistryAbsolute,
                new { table, rows = stats.Rows, min = stats.MinTime, max = stats.MaxTime });
            return new TickCoverage(true, stats.Rows, stats.MinTime, stats.MaxTime);
        }

        public IReadOnlyList<TickTableStatRow> GetRegistry()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            EnsureRegistryTable(conn);
            return conn.Query<TickTableStatRow>(Sql.SelectRegistry).AsList();
        }

        public long InsertTicks(string table, IReadOnlyList<Tick> ticks)
        {
            if (ticks.Count == 0) return 0;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            var staging = $"stage_{Guid.NewGuid():N}";
            long written;
            try
            {
                conn.Execute(Sql.CreateStagingTable(staging));

                using (var writer = conn.BeginBinaryImport(Sql.CopyInto(staging)))
                {
                    foreach (var t in ticks)
                    {
                        writer.StartRow();
                        writer.Write(DateTime.SpecifyKind(t.Timestamp, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
                        writer.Write(t.Bid, NpgsqlDbType.Double);
                        writer.Write(t.Ask, NpgsqlDbType.Double);
                    }
                    writer.Complete();
                }

                written = conn.Execute(Sql.InsertFromStagingSkipDuplicates(table, staging));
            }
            finally
            {
                conn.Execute(Sql.DropStagingTable(staging));
            }

            EnsureRegistryTable(conn);
            conn.Execute(Sql.UpsertRegistryDelta,
                new { table, n = written, min = ticks[0].Timestamp, max = ticks[^1].Timestamp });

            return written;
        }

        public void LogBackfill(
            string table, long symbolCode, string symbolName,
            DateTime backfillFromUtc, DateTime backfillToUtc, long recordCount, BackfillTrigger trigger)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            EnsureTable(conn, Sql.CreateBackfillLogTable);
            conn.Execute(Sql.InsertBackfillLog, new
            {
                table,
                symbolCode,
                symbolName,
                from = DateTime.SpecifyKind(backfillFromUtc, DateTimeKind.Utc),
                to = DateTime.SpecifyKind(backfillToUtc, DateTimeKind.Utc),
                recordCount,
                trigger = trigger.ToString()
            });

            _logger.LogInformation(
                "Backfill logged: {Table} {SymbolName} ({SymbolCode}) {From:u}..{To:u} -> {Count} row(s), triggered by {Trigger}.",
                table, symbolName, symbolCode, backfillFromUtc, backfillToUtc, recordCount, trigger);
        }

        public IReadOnlyList<BackfillLogEntry> GetBackfillLog()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            EnsureTable(conn, Sql.CreateBackfillLogTable);
            return conn.Query<BackfillLogEntry>(Sql.SelectBackfillLog).AsList();
        }

        private static void EnsureRegistryTable(NpgsqlConnection conn) => EnsureTable(conn, Sql.CreateRegistryTable);

        /// <summary>Runs idempotent DDL, tolerating the race where a concurrent caller wins CREATE ... IF NOT EXISTS first.</summary>
        private static void EnsureTable(NpgsqlConnection conn, string ddl)
        {
            try
            {
                conn.Execute(ddl);
            }
            catch (PostgresException ex) when (ex.SqlState is "42P07" or "42710")
            {
            }
        }

        private sealed record RegistryRow(long RowCount, DateTime? MinTimestamp, DateTime? MaxTimestamp);
        private sealed record StatsRow(long Rows, DateTime? MinTime, DateTime? MaxTime);

        private static class Sql
        {
            public const string TableExists =
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @table)";

            public static string CreateTickTable(string table) =>
                $@"CREATE EXTENSION IF NOT EXISTS timescaledb;
                   CREATE TABLE IF NOT EXISTS ""{table}"" (
                       ""timestamp"" TIMESTAMPTZ NOT NULL UNIQUE,
                       ""bid"" DOUBLE PRECISION NOT NULL,
                       ""ask"" DOUBLE PRECISION NOT NULL
                   );
                   SELECT create_hypertable('{table}', 'timestamp', if_not_exists => true);";

            public static string CopyInto(string table) =>
                $@"COPY ""{table}"" (""timestamp"", ""bid"", ""ask"") FROM STDIN (FORMAT BINARY)";

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

            public static string DropStagingTable(string staging) =>
                $@"DROP TABLE IF EXISTS ""{staging}""";

            public static string SelectTableStats(string table) =>
                $"SELECT COUNT(*) AS \"Rows\", MIN(\"timestamp\") AS \"MinTime\", MAX(\"timestamp\") AS \"MaxTime\" FROM \"{table}\"";

            // Fresh registries get the symbol column; pre-existing ones are patched with the ALTER.
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

            public const string SelectTableForSymbol =
                $@"SELECT ""table_name"" FROM ""{RegistryTable}"" WHERE ""symbol"" = @symbol ORDER BY ""table_name"" LIMIT 1";

            public const string EnsureRegistryRow =
                $@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""refreshed_at"")
                   VALUES (@table, 0, now())
                   ON CONFLICT (""table_name"") DO NOTHING";

            public const string UpdateSymbol =
                $@"UPDATE ""{RegistryTable}"" SET ""symbol"" = @symbol, ""refreshed_at"" = now() WHERE ""table_name"" = @table";

            public const string InsertRowWithSymbol =
                $@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""min_timestamp"", ""max_timestamp"", ""symbol"", ""refreshed_at"")
                   VALUES (@table, @rows, @min, @max, @symbol, now())
                   ON CONFLICT (""table_name"") DO UPDATE SET
                       ""row_count"" = EXCLUDED.""row_count"",
                       ""min_timestamp"" = EXCLUDED.""min_timestamp"",
                       ""max_timestamp"" = EXCLUDED.""max_timestamp"",
                       ""symbol"" = EXCLUDED.""symbol"",
                       ""refreshed_at"" = now()";

            public const string SelectRegistryRow =
                $@"SELECT ""row_count"" AS ""RowCount"", ""min_timestamp"" AS ""MinTimestamp"", ""max_timestamp"" AS ""MaxTimestamp""
                   FROM ""{RegistryTable}"" WHERE ""table_name"" = @table";

            public const string SelectRegistry =
                $@"SELECT ""table_name"" AS ""Table"", ""symbol"" AS ""Symbol"", ""row_count"" AS ""Rows"",
                          ""min_timestamp"" AS ""MinUtc"", ""max_timestamp"" AS ""MaxUtc"", ""refreshed_at"" AS ""RefreshedAt""
                   FROM ""{RegistryTable}"" ORDER BY ""table_name""";

            public const string UpsertRegistryAbsolute =
                $@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""min_timestamp"", ""max_timestamp"", ""refreshed_at"")
                   VALUES (@table, @rows, @min, @max, now())
                   ON CONFLICT (""table_name"") DO UPDATE SET
                       ""row_count"" = EXCLUDED.""row_count"",
                       ""min_timestamp"" = EXCLUDED.""min_timestamp"",
                       ""max_timestamp"" = EXCLUDED.""max_timestamp"",
                       ""refreshed_at"" = EXCLUDED.""refreshed_at""";

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
                   VALUES (@table, @symbolCode, @symbolName, @from, @to, @recordCount, @trigger)";

            public static string SelectBackfillLog =>
                $@"SELECT ""id"" AS ""Id"", ""tick_table_name"" AS ""TickTableName"", ""symbol_code"" AS ""SymbolCode"",
                          ""symbol_name"" AS ""SymbolName"", ""backfill_from"" AS ""BackfillFrom"", ""backfill_to"" AS ""BackfillTo"",
                          ""record_count"" AS ""RecordCount"", ""trigger_entity"" AS ""TriggerEntity"", ""triggered_at"" AS ""TriggeredAtUtc""
                   FROM ""{BackfillLogTable}"" ORDER BY ""id"" DESC";
        }
    }
}
