using System;
using System.Collections.Generic;
using Npgsql;

namespace Poink.Sim.Repositories;

internal sealed class TickRepository : ITickRepository
{
    // Matches the hardcoded connection string in Poink.Trader.cs (local dev Postgres).
    const string ConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=poink";

    public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        var tableNames = new List<string>();
        using (var cmd = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'ticks_%' ORDER BY table_name", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read()) tableNames.Add(reader.GetString(0));
        }

        var result = new List<(string, long, DateTime?, DateTime?)>();
        foreach (var table in tableNames)
        {
            using var cmd = new NpgsqlCommand(
                $"SELECT COUNT(*), MIN(\"timestamp\"), MAX(\"timestamp\") FROM \"{table}\"", conn);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            long rows = reader.GetInt64(0);
            DateTime? min = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            DateTime? max = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            result.Add((table, rows, min, max));
        }
        return result;
    }

    // Streams ticks in chronological order for a given table (and optional time range).
    public IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        var sql = $"SELECT \"timestamp\", \"bid\", \"ask\" FROM \"{table}\" WHERE 1=1";
        if (from.HasValue) sql += " AND \"timestamp\" >= @from";
        if (to.HasValue)   sql += " AND \"timestamp\" <  @to";
        sql += " ORDER BY \"timestamp\"";

        using var cmd = new NpgsqlCommand(sql, conn);
        if (from.HasValue) cmd.Parameters.AddWithValue("from", from.Value);
        if (to.HasValue)   cmd.Parameters.AddWithValue("to", to.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new Tick(reader.GetDateTime(0), reader.GetDouble(1), reader.GetDouble(2));
        }
    }
}
