using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using NpgsqlTypes;
using Npgsql;
using Serilog;
using Serilog.Core;

namespace cAlgo.Robots;

[Robot(AccessRights = AccessRights.FullAccess, AddIndicators = true)]
public class PoinkTrader : Robot
{
    private const string ConnectionString = "Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=Poink.MarketData";
    private const string RegistryTable = "tick_table_stats";
    private const int BatchSize = 1000;
    private const int LogInterval = 100_000;

    [Parameter("Override Table Name", DefaultValue = false, Group = "Table")]
    public bool OverrideTableName { get; set; }

    [Parameter("Custom Table Name", DefaultValue = "", Group = "Table")]
    public string CustomTableName { get; set; }

    private string _tableName;
    private NpgsqlConnection _connection;
    private NpgsqlCommand _mergeCommand;
    private NpgsqlCommand _truncateTempCommand;
    private List<(DateTime Timestamp, double Bid, double Ask)> _buffer;
    private long _totalTicks;
    private long _milestonesLogged;
    private DateTime _startedAtUtc;
    private Logger _logger;

    protected override void OnStart()
    {
        var logDirectory = Path.Combine(SourceDirectory(), "logs");
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "poink-trader-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _tableName = OverrideTableName && !string.IsNullOrWhiteSpace(CustomTableName)
            ? CustomTableName
            : ForSymbol(SymbolName, Account.IsLive);
        _buffer = new List<(DateTime, double, double)>(BatchSize);
        _totalTicks = 0;
        _milestonesLogged = 0;
        _startedAtUtc = DateTime.UtcNow;

        _logger.Information("Starting {Symbol} tick capture into \"{TableName}\" (batch size {BatchSize}).", SymbolName, _tableName, BatchSize);

        _connection = new NpgsqlConnection(ConnectionString);
        _connection.Open();

        try
        {
            using (var createCommand = new NpgsqlCommand(
                $@"CREATE EXTENSION IF NOT EXISTS timescaledb;
                    CREATE TABLE IF NOT EXISTS ""{_tableName}"" (
                    ""timestamp"" TIMESTAMPTZ NOT NULL UNIQUE,
                    ""bid"" DOUBLE PRECISION NOT NULL,
                    ""ask"" DOUBLE PRECISION NOT NULL
                );
                    CREATE TABLE IF NOT EXISTS ""{RegistryTable}"" (
                    ""table_name"" TEXT PRIMARY KEY,
                    ""row_count"" BIGINT NOT NULL,
                    ""min_timestamp"" TIMESTAMPTZ,
                    ""max_timestamp"" TIMESTAMPTZ,
                    ""symbol"" TEXT,
                    ""refreshed_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
                );
                    ALTER TABLE ""{RegistryTable}"" ADD COLUMN IF NOT EXISTS ""symbol"" TEXT;",
                _connection))
            {
                createCommand.ExecuteNonQuery();
            }

            _logger.Information("Created table \"{TableName}\".", _tableName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create table \"{TableName}\".", _tableName);
            Stop();
            return;
        }

        using (var hypertableCommand = new NpgsqlCommand(
            $@"SELECT create_hypertable('{_tableName}', 'timestamp', if_not_exists => true);",
            _connection))
        {
            try
            {
                hypertableCommand.ExecuteNonQuery();
            }
            catch (PostgresException ex)
            {
                // A table already holding ticks needs a deliberate, manual migrate_data => true run; never triggered from OnStart.
                _logger.Warning("\"{TableName}\" not converted to a TimescaleDB hypertable: {Reason}", _tableName, ex.MessageText);
            }
        }

        try
        {
            using (var createTempCommand = new NpgsqlCommand(
                @"CREATE TEMP TABLE tmp_ticks (
                    ""timestamp"" TIMESTAMPTZ NOT NULL,
                    ""bid"" DOUBLE PRECISION NOT NULL,
                    ""ask"" DOUBLE PRECISION NOT NULL
                );",
                _connection))
            {
                createTempCommand.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create temp table \"tmp_ticks\".");
            Stop();
            return;
        }

        // Kept open and reused for the whole run: OnTick flushes every BatchSize
        // ticks, so this avoids a reconnect per batch on the hot path.
        _truncateTempCommand = new NpgsqlCommand(@"TRUNCATE TABLE tmp_ticks;", _connection);
        _mergeCommand = new NpgsqlCommand(
            $@"INSERT INTO ""{_tableName}"" (""timestamp"", ""bid"", ""ask"")
                SELECT ""timestamp"", ""bid"", ""ask"" FROM tmp_ticks
                ON CONFLICT (""timestamp"") DO NOTHING;",
            _connection);
    }

    protected override void OnTick()
    {
        _buffer.Add((Server.TimeInUtc, Symbol.Bid, Symbol.Ask));
        _totalTicks++;

        // Flush as soon as a batch fills instead of accumulating the whole
        // run in memory -- keeps memory flat whether the run is 1K or 100M ticks.
        if (_buffer.Count >= BatchSize)
            FlushBatch();

        var milestone = _totalTicks / LogInterval;
        if (milestone > _milestonesLogged)
        {
            _milestonesLogged = milestone;
            var elapsed = DateTime.UtcNow - _startedAtUtc;
            var rate = elapsed.TotalSeconds > 0 ? _totalTicks / elapsed.TotalSeconds : 0;
            _logger.Information("{Symbol}: {TotalTicks:N0} ticks processed ({Elapsed:hh\\:mm\\:ss} elapsed, {Rate:N0} ticks/sec).", SymbolName, _totalTicks, elapsed, rate);
        }
    }

    protected override void OnStop()
    {
        if (_buffer.Count > 0)
            FlushBatch();

        UpdateRegistry();

        _mergeCommand?.Dispose();
        _truncateTempCommand?.Dispose();
        _connection?.Dispose();

        _logger?.Information("Finished saving {TotalTicks:N0} ticks for {Symbol}.", _totalTicks, SymbolName);
        _logger?.Dispose();
    }

    // Keeps tick_table_stats (read by Poink.Sim's TickRepository) in step with this table
    // without requiring a manual "Recount Tables" pass in the UI.
    private void UpdateRegistry()
    {
        try
        {
            long rowCount;
            DateTime? minTime;
            DateTime? maxTime;

            using (var statsCommand = new NpgsqlCommand(
                $@"SELECT COUNT(*), MIN(""timestamp""), MAX(""timestamp"") FROM ""{_tableName}"";",
                _connection))
            using (var reader = statsCommand.ExecuteReader())
            {
                reader.Read();
                rowCount = reader.GetInt64(0);
                minTime = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                maxTime = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            }

            using (var upsertCommand = new NpgsqlCommand(
                $@"INSERT INTO ""{RegistryTable}"" (""table_name"", ""row_count"", ""min_timestamp"", ""max_timestamp"", ""symbol"", ""refreshed_at"")
                    VALUES (@table, @rows, @min, @max, @symbol, now())
                    ON CONFLICT (""table_name"") DO UPDATE SET
                        ""row_count"" = EXCLUDED.""row_count"",
                        ""min_timestamp"" = EXCLUDED.""min_timestamp"",
                        ""max_timestamp"" = EXCLUDED.""max_timestamp"",
                        ""symbol"" = EXCLUDED.""symbol"",
                        ""refreshed_at"" = EXCLUDED.""refreshed_at"";",
                _connection))
            {
                upsertCommand.Parameters.AddWithValue("table", _tableName);
                upsertCommand.Parameters.AddWithValue("rows", rowCount);
                upsertCommand.Parameters.AddWithValue("min", NpgsqlDbType.TimestampTz, (object)minTime ?? DBNull.Value);
                upsertCommand.Parameters.AddWithValue("max", NpgsqlDbType.TimestampTz, (object)maxTime ?? DBNull.Value);
                upsertCommand.Parameters.AddWithValue("symbol", SymbolName);
                upsertCommand.ExecuteNonQuery();
            }

            _logger?.Information(
                "Updated {RegistryTable} for \"{TableName}\": {RowCount:N0} rows ({MinTime} to {MaxTime}).",
                RegistryTable, _tableName, rowCount, minTime, maxTime);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Failed to update {RegistryTable} for \"{TableName}\".", RegistryTable, _tableName);
        }
    }

    private void FlushBatch()
    {
        try
        {
            using (var writer = _connection.BeginBinaryImport(
                "COPY tmp_ticks (\"timestamp\", \"bid\", \"ask\") FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var tick in _buffer)
                {
                    writer.StartRow();
                    writer.Write(tick.Timestamp, NpgsqlDbType.TimestampTz);
                    writer.Write(tick.Bid, NpgsqlDbType.Double);
                    writer.Write(tick.Ask, NpgsqlDbType.Double);
                }

                writer.Complete();
            }

            _mergeCommand.ExecuteNonQuery();
            _truncateTempCommand.ExecuteNonQuery();
            _buffer.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to insert tick batch into \"{TableName}\".", _tableName);
            Stop();
        }
    }

    private static string ForSymbol(string symbolName, bool isLive)
    {
        var cleaned = new string(symbolName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var prefix = isLive ? "live" : "demo";
        return $"{prefix}_ticks_{cleaned}";
    }

    // CallerFilePath resolves to this .cs file's own directory, regardless of the compiled output location.
    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path);
}
