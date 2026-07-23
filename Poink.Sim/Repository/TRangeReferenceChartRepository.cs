using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Poink.Sim.Repository;

internal sealed class TRangeReferenceChartRepository : ITRangeReferenceChartRepository
{
    // Same "Poink.Sim" database as TRangeChartRepository (see
    // ITRangeReferenceChartRepository's doc comment) -- own table within it, so
    // EnsureDatabaseExists/CREATE DATABASE duplicates TRangeChartRepository's
    // rather than sharing it, matching this codebase's existing per-repository
    // self-containment.
    const string DatabaseName = "Poink.Sim";
    const string MaintenanceConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";
    const string ConnectionString = $"Host=localhost;Port=5432;Username=postgres;Password=postgres;Database={DatabaseName}";
    const string TableName = "trange_reference_charts";

    private readonly ILogger<TRangeReferenceChartRepository> _logger;

    public TRangeReferenceChartRepository(ILogger<TRangeReferenceChartRepository> logger) => _logger = logger;

    public long SaveTRangeReferenceChart(
        string label, string symbol, DateTime startTimeUtc, DateTime endTimeUtc, double barMinutes, int barCount,
        DateTime referenceStartTimeUtc, DateTime referenceEndTimeUtc, int referenceBarCount)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);

        long id = conn.ExecuteScalar<long>(Sql.InsertChart, new
        {
            label, symbol, startTimeUtc, endTimeUtc, barMinutes, barCount,
            referenceStartTimeUtc, referenceEndTimeUtc, referenceBarCount,
        });
        _logger.LogInformation(
            "Saved T-Range Reference Chart on {Symbol} [{Start} - {End}] (reference [{RefStart} - {RefEnd}]) as id {Id} labeled {Label}",
            symbol, startTimeUtc, endTimeUtc, referenceStartTimeUtc, referenceEndTimeUtc, id, label);
        return id;
    }

    public List<TRangeReferenceChart> ListTRangeReferenceCharts()
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);

        var result = conn.Query<TRangeReferenceChart>(Sql.SelectCharts).ToList();
        _logger.LogInformation("Listed {ChartCount} saved T-Range Reference Charts", result.Count);
        return result;
    }

    public void DeleteTRangeReferenceChart(long id)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);
        conn.Execute(Sql.DeleteChart, new { id });
        _logger.LogInformation("Deleted saved T-Range Reference Chart id {Id}", id);
    }

    private void EnsureDatabaseExists()
    {
        using var conn = new NpgsqlConnection(MaintenanceConnectionString);
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

        // DatabaseName/TableName are compile-time constants, never user input, so
        // interpolating them as identifiers here is safe (same reasoning as
        // TRangeChartRepository.Sql).
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
                   ""reference_start_time_utc"" TIMESTAMPTZ NOT NULL,
                   ""reference_end_time_utc"" TIMESTAMPTZ NOT NULL,
                   ""reference_bar_count"" INTEGER NOT NULL,
                   ""saved_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );";

        public const string InsertChart =
            $@"INSERT INTO ""{TableName}""
                   (""label"", ""symbol"", ""start_time_utc"", ""end_time_utc"", ""bar_minutes"", ""bar_count"",
                    ""reference_start_time_utc"", ""reference_end_time_utc"", ""reference_bar_count"", ""saved_at"")
               VALUES
                   (@label, @symbol, @startTimeUtc, @endTimeUtc, @barMinutes, @barCount,
                    @referenceStartTimeUtc, @referenceEndTimeUtc, @referenceBarCount, now())
               RETURNING ""id""";

        public const string SelectCharts =
            $@"SELECT ""id"" AS ""Id"", ""label"" AS ""Label"", ""symbol"" AS ""Symbol"",
                      ""start_time_utc"" AS ""StartTimeUtc"", ""end_time_utc"" AS ""EndTimeUtc"",
                      ""bar_minutes"" AS ""BarMinutes"", ""bar_count"" AS ""BarCount"",
                      ""reference_start_time_utc"" AS ""ReferenceStartTimeUtc"",
                      ""reference_end_time_utc"" AS ""ReferenceEndTimeUtc"",
                      ""reference_bar_count"" AS ""ReferenceBarCount"",
                      ""saved_at"" AS ""SavedAt""
               FROM ""{TableName}"" ORDER BY ""saved_at"" DESC";

        public const string DeleteChart = $@"DELETE FROM ""{TableName}"" WHERE ""id"" = @id";
    }
}
