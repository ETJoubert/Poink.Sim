using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Poink.Sim.Repository;

internal sealed class BrainRepository : IBrainRepository
{
    // Own database, separate from TickRepository's Poink.MarketData -- see
    // IBrainRepository's doc comment. "postgres" is Postgres's always-present
    // maintenance db, used only to CREATE DATABASE this one if it's missing
    // (Postgres has no CREATE DATABASE IF NOT EXISTS, so pg_database is
    // checked first -- same idea as EnsureRegistryTable in TickRepository,
    // one level up).
    const string DatabaseName = "Poink.Sim";
    const string MaintenanceConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";
    const string ConnectionString = $"Host=localhost;Port=5432;Username=postgres;Password=postgres;Database={DatabaseName}";
    const string TableName = "trained_brains";

    private readonly ILogger<BrainRepository> _logger;

    public BrainRepository(ILogger<BrainRepository> logger) => _logger = logger;

    public long SaveBrain(
        string name, string brainName, string strategyName, string dataset, BrainTrainingParameters parameters,
        long weightCount, double avgReward, byte[] weights)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);

        string parametersJson = JsonSerializer.Serialize(parameters);
        long id = conn.ExecuteScalar<long>(Sql.InsertBrain, new
        {
            name, brainName, strategyName, dataset, parametersJson, weightCount, avgReward, weights
        });
        _logger.LogInformation("Saved brain {Name} ({BrainName}/{StrategyName}) as id {Id}", name, brainName, strategyName, id);
        return id;
    }

    public List<SavedBrainSummary> ListBrains()
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);

        var result = conn.Query<BrainRow>(Sql.SelectBrainRows)
            .Select(r => new SavedBrainSummary(
                r.Id, r.Name, r.BrainName, r.StrategyName, r.Dataset,
                JsonSerializer.Deserialize<BrainTrainingParameters>(r.ParametersJson) ?? new BrainTrainingParameters(),
                r.WeightCount, r.AvgReward, r.SavedAt))
            .ToList();
        _logger.LogInformation("Listed {BrainCount} saved brains", result.Count);
        return result;
    }

    public byte[]? LoadWeights(long id)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);
        var weights = conn.QuerySingleOrDefault<byte[]>(Sql.SelectWeights, new { id });
        if (weights is null)
            _logger.LogWarning("No weights found for brain id {Id}", id);
        else
            _logger.LogInformation("Loaded weights for brain id {Id} ({ByteCount} bytes)", id, weights.Length);
        return weights;
    }

    public void DeleteBrain(long id)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        EnsureTable(conn);
        conn.Execute(Sql.DeleteBrain, new { id });
        _logger.LogInformation("Deleted brain id {Id}", id);
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

    // The very first prototype of this table used brain_name as its primary
    // key (one upserted row per architecture, no history). An install still
    // carrying that shape is safe to drop and rebuild -- it could only ever
    // have held one row per architecture, and nothing shipped against it.
    // Later additive columns (like "dataset") instead migrate in place via
    // ADD COLUMN IF NOT EXISTS, since by then real saved brains can exist.
    private void EnsureTable(NpgsqlConnection conn)
    {
        bool hasOldShape = conn.ExecuteScalar<bool>(Sql.OldShapeExists);
        if (hasOldShape)
        {
            _logger.LogWarning("Dropping legacy-shape {TableName} table before recreating it", TableName);
            conn.Execute(Sql.DropTable);
        }

        conn.Execute(Sql.CreateTable);
        conn.Execute(Sql.AddDatasetColumn);
    }

    private sealed record BrainRow(
        long Id, string Name, string BrainName, string StrategyName, string Dataset,
        string ParametersJson, long WeightCount, double AvgReward, System.DateTime SavedAt);

    // ── SQL ──
    private static class Sql
    {
        public const string DatabaseExists = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";

        // DatabaseName/TableName are compile-time constants, never user input,
        // so interpolating them as identifiers here is safe (same reasoning as
        // TickRepository.Sql.SelectTableStats).
        public const string CreateDatabase = $"CREATE DATABASE \"{DatabaseName}\"";

        public const string OldShapeExists =
            $@"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{TableName}' AND column_name = 'brain_name')
               AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{TableName}' AND column_name = 'id')";

        public const string DropTable = $@"DROP TABLE ""{TableName}""";

        public const string CreateTable =
            $@"CREATE TABLE IF NOT EXISTS ""{TableName}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""name"" TEXT NOT NULL,
                   ""brain_name"" TEXT NOT NULL,
                   ""strategy_name"" TEXT NOT NULL,
                   ""dataset"" TEXT NOT NULL DEFAULT '',
                   ""parameters"" JSONB NOT NULL,
                   ""weight_count"" BIGINT NOT NULL,
                   ""avg_reward"" DOUBLE PRECISION NOT NULL,
                   ""weights"" BYTEA NOT NULL,
                   ""saved_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );";

        // Additive migration for an install that already has the id-keyed shape
        // but predates the "dataset" column -- existing rows backfill to ''.
        public const string AddDatasetColumn =
            $@"ALTER TABLE ""{TableName}"" ADD COLUMN IF NOT EXISTS ""dataset"" TEXT NOT NULL DEFAULT ''";

        public const string InsertBrain =
            $@"INSERT INTO ""{TableName}"" (""name"", ""brain_name"", ""strategy_name"", ""dataset"", ""parameters"", ""weight_count"", ""avg_reward"", ""weights"", ""saved_at"")
               VALUES (@name, @brainName, @strategyName, @dataset, @parametersJson::jsonb, @weightCount, @avgReward, @weights, now())
               RETURNING ""id""";

        public const string SelectBrainRows =
            $@"SELECT ""id"" AS ""Id"", ""name"" AS ""Name"", ""brain_name"" AS ""BrainName"", ""strategy_name"" AS ""StrategyName"",
                      ""dataset"" AS ""Dataset"", ""parameters""::text AS ""ParametersJson"", ""weight_count"" AS ""WeightCount"",
                      ""avg_reward"" AS ""AvgReward"", ""saved_at"" AS ""SavedAt""
               FROM ""{TableName}"" ORDER BY ""saved_at"" DESC";

        public const string SelectWeights = $@"SELECT ""weights"" FROM ""{TableName}"" WHERE ""id"" = @id";

        public const string DeleteBrain = $@"DELETE FROM ""{TableName}"" WHERE ""id"" = @id";
    }
}
