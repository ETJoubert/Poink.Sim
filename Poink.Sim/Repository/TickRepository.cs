using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dapper;
using Npgsql;

namespace Poink.Sim.Repository;

internal sealed class TickRepository : ITickRepository
{
    // Ticks live in the separate Poink.MarketData database (written by the
    // Poink.Trader cAlgo robot, which creates one "ticks_<symbol>" table per
    // symbol with an identical (timestamp, bid, ask) shape -- see
    // Poink.Trader.cs, left untouched). Table names are only known at runtime,
    // so Dapper (SQL text + row mapping) is used instead of a static EF model.
    const string ConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=Poink.MarketData";

    // Postgres-side cache of table stats -- COUNT(*)/MIN/MAX over a ticks_*
    // table is a full scan and too slow to run on every read (see
    // SimulationPage.razor.cs's OnInitialized comment). RefreshTickTableStats()
    // is the only thing that ever rescans and writes here.
    const string RegistryTable = "tick_table_stats";

    private sealed record TickTableStats(long Rows, DateTime? MinTime, DateTime? MaxTime);
    private sealed record TickTableStatsRow(string TableName, long RowCount, DateTime? MinTimestamp, DateTime? MaxTimestamp);

    // In-process cache of the last ListTickTables()/RefreshTickTableStats() result.
    // TickRepository is registered as a singleton (see Program.cs), so this is
    // shared by every page/circuit in the process -- once one page loads or
    // recounts the table list, every other page can reuse it via CachedTables
    // instead of hitting Postgres again on its own OnInitialized.
    private readonly Lock _cacheLock = new();
    private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>? _cachedTables;

    public bool HasCachedTables { get { lock (_cacheLock) return _cachedTables is not null; } }

    public IReadOnlyList<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> CachedTables
    {
        get { lock (_cacheLock) return _cachedTables ?? []; }
    }

    public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureRegistryTable(conn);

        var rows = conn.Query<TickTableStatsRow>(Sql.SelectRegistryRows);
        var result = rows.Select(r => (r.TableName, r.RowCount, r.MinTimestamp, r.MaxTimestamp)).ToList();
        lock (_cacheLock) { _cachedTables = result; }
        return result;
    }

    public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> RefreshTickTableStats()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureRegistryTable(conn);

        var tableNames = conn.Query<string>(Sql.SelectTickTableNames).ToList();

        foreach (var table in tableNames)
        {
            var stats = conn.QuerySingle<TickTableStats>(Sql.SelectTableStats(table));
            conn.Execute(Sql.UpsertRegistryRow, new { table, rows = stats.Rows, min = stats.MinTime, max = stats.MaxTime });
        }

        // Drop registry rows for tables that no longer exist -- `<> ALL(@names)`
        // is vacuously true for every row when tableNames is empty, so this also
        // correctly empties the registry if every ticks_* table has been dropped.
        conn.Execute(Sql.DeleteStaleRegistryRows, new { names = tableNames.ToArray() });

        return ListTickTables();
    }

    // Streams ticks in chronological order for a given table (and optional time range).
    // Kept as a lazy iterator (Dapper's buffered: false) -- the connection stays open
    // only while a caller is actually enumerating, and is closed via `using` once the
    // iterator is disposed/exhausted (see README.md's "Playback design" section).
    public IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        var sql = Sql.SelectTicks(table, from.HasValue, to.HasValue);
        foreach (var tick in conn.Query<Tick>(sql, new { from, to }, buffered: false))
        {
            yield return tick;
        }
    }

    private static void EnsureRegistryTable(NpgsqlConnection conn) => conn.Execute(Sql.CreateRegistryTable);

    // ── SQL ──
    // All query text lives here, named by what it does, so the methods above
    // read as plain calls instead of inline strings.
    private static class Sql
    {
        public const string SelectTickTableNames =
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'ticks_%' ORDER BY table_name";

        public const string CreateRegistryTable =
            $@"CREATE TABLE IF NOT EXISTS ""{RegistryTable}"" (
                   ""table_name"" TEXT PRIMARY KEY,
                   ""row_count"" BIGINT NOT NULL,
                   ""min_timestamp"" TIMESTAMPTZ,
                   ""max_timestamp"" TIMESTAMPTZ,
                   ""refreshed_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );";

        public const string SelectRegistryRows =
            $@"SELECT ""table_name"" AS ""TableName"", ""row_count"" AS ""RowCount"",
                      ""min_timestamp"" AS ""MinTimestamp"", ""max_timestamp"" AS ""MaxTimestamp""
               FROM ""{RegistryTable}"" ORDER BY ""table_name""";

        public const string UpsertRegistryRow =
            $@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""min_timestamp"", ""max_timestamp"", ""refreshed_at"")
               VALUES (@table, @rows, @min, @max, now())
               ON CONFLICT (""table_name"") DO UPDATE SET
                   ""row_count"" = EXCLUDED.""row_count"",
                   ""min_timestamp"" = EXCLUDED.""min_timestamp"",
                   ""max_timestamp"" = EXCLUDED.""max_timestamp"",
                   ""refreshed_at"" = EXCLUDED.""refreshed_at""";

        public const string DeleteStaleRegistryRows =
            $@"DELETE FROM ""{RegistryTable}"" WHERE ""table_name"" <> ALL(@names)";

        // table comes from information_schema (see SelectTickTableNames), never
        // from user input, so interpolating it as an identifier is safe here.
        public static string SelectTableStats(string table) =>
            $"SELECT COUNT(*) AS \"Rows\", MIN(\"timestamp\") AS \"MinTime\", MAX(\"timestamp\") AS \"MaxTime\" FROM \"{table}\"";

        public static string SelectTicks(string table, bool hasFrom, bool hasTo)
        {
            var sql = $"SELECT \"timestamp\", \"bid\", \"ask\" FROM \"{table}\" WHERE 1=1";
            if (hasFrom)
                sql += " AND \"timestamp\" >= @from";
            if (hasTo)
                sql += " AND \"timestamp\" <  @to";
            return sql + " ORDER BY \"timestamp\"";
        }
    }
}
