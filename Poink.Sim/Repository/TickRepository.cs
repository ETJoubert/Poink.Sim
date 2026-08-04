using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Poink.Sim.Repository;

internal sealed class TickRepository : ITickRepository
{

    private readonly string _connectionString;


    const string RegistryTable = "tick_table_stats";

    private sealed record TickTableStats(long Rows, DateTime? MinTime, DateTime? MaxTime);
    private sealed record TickTableStatsRow(string TableName, long RowCount, DateTime? MinTimestamp, DateTime? MaxTimestamp);


    private readonly Lock _cacheLock = new();
    private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>? _cachedTables;

    private readonly ILogger<TickRepository> _logger;

    public TickRepository(ILogger<TickRepository> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("PoinkMarketData")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'PoinkMarketData'. Add ConnectionStrings:PoinkMarketData " +
                "to appsettings, appsettings.Secrets.json, or environment variables.");
    }

    public bool HasCachedTables { get { lock (_cacheLock) return _cachedTables is not null; } }

    public IReadOnlyList<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> CachedTables
    {
        get { lock (_cacheLock) return _cachedTables ?? []; }
    }

    public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        EnsureRegistryTable(conn);

        var rows = conn.Query<TickTableStatsRow>(Sql.SelectRegistryRows);
        var result = rows.Select(r => (r.TableName, r.RowCount, r.MinTimestamp, r.MaxTimestamp)).ToList();
        lock (_cacheLock)
        { _cachedTables = result; }
        _logger.LogInformation("Listed {TableCount} tick tables", result.Count);
        return result;
    }

    public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> RefreshTickTableStats()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        EnsureRegistryTable(conn);

        var tableNames = conn.Query<string>(Sql.SelectTickTableNames).ToList();

        foreach (var table in tableNames)
        {
            var stats = conn.QuerySingle<TickTableStats>(Sql.SelectTableStats(table));
            conn.Execute(Sql.UpsertRegistryRow, new { table, rows = stats.Rows, min = stats.MinTime, max = stats.MaxTime });
        }


        conn.Execute(Sql.DeleteStaleRegistryRows, new { names = tableNames.ToArray() });

        _logger.LogInformation("Refreshed tick table stats for {TableCount} tables", tableNames.Count);
        return ListTickTables();
    }


    public IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        //_logger.LogInformation("Opening tick stream for {Table} (from={From}, to={To})", table, from, to);

        var sql = Sql.SelectTicks(table, from.HasValue, to.HasValue);
        foreach (var tick in conn.Query<Tick>(sql, new { from, to }, buffered: false))
        {
            yield return tick;
        }
    }


    public List<Tick> ReadWindow(string table, DateTime start, int count)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        var sql = Sql.SelectWindow(table);
        return conn.Query<Tick>(sql, new { start, count }).ToList();
    }

    public List<Tick> ReadRecentTicks(string table, DateTime beforeUtc, int count)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        var sql = Sql.SelectRecentTicks(table);
        var rows = conn.Query<Tick>(sql, new { beforeUtc, count }).ToList();
        rows.Reverse();
        return rows;
    }


    public long CountTicks(string table, DateTime from, DateTime to)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        var sql = Sql.CountTicks(table);
        long count = conn.QuerySingle<long>(sql, new { from, to });
        _logger.LogInformation("Counted ticks for {Table} between {From} and {To}: {Count}", table, from, to, count);
        return count;
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

        public static string SelectWindow(string table) =>
            $"SELECT \"timestamp\", \"bid\", \"ask\" FROM \"{table}\" WHERE \"timestamp\" >= @start ORDER BY \"timestamp\" LIMIT @count";

        public static string SelectRecentTicks(string table) =>
            $"SELECT \"timestamp\", \"bid\", \"ask\" FROM \"{table}\" WHERE \"timestamp\" <= @beforeUtc ORDER BY \"timestamp\" DESC LIMIT @count";

        public static string CountTicks(string table) =>
            $"SELECT COUNT(*) FROM \"{table}\" WHERE \"timestamp\" >= @from AND \"timestamp\" < @to";
    }
}
