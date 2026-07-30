using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Repository;

internal sealed class NetworkRepository : INetworkRepository
{
    // Recipe's EncoderKinds/ArchitectureKind serialize as names, not ordinal
    // ints -- same reasoning as ModelRepository's own copy of this option set.
    private static readonly JsonSerializerOptions RecipeJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    // Same "Poink.Sim" database as ModelRepository -- a network is just a
    // config, not market data, and belongs alongside trained_models rather
    // than in its own database.
    const string DatabaseName = "Poink.Sim";
    const string TableName = "networks";

    private readonly ILogger<NetworkRepository> _logger;
    private readonly string _maintenanceConnectionString;
    private readonly string _connectionString;

    public NetworkRepository(ILogger<NetworkRepository> logger, IConfiguration configuration)
    {
        _logger = logger;
        _maintenanceConnectionString = configuration.GetConnectionString("PoinkSimMaintenance")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'PoinkSimMaintenance'. Add ConnectionStrings:PoinkSimMaintenance " +
                "to appsettings, appsettings.Secrets.json, or environment variables.");
        _connectionString = configuration.GetConnectionString("PoinkSim")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'PoinkSim'. Add ConnectionStrings:PoinkSim " +
                "to appsettings, appsettings.Secrets.json, or environment variables.");
    }

    public long SaveNetwork(string name, BrainRecipe recipe)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        EnsureTable(conn);

        string recipeJson = JsonSerializer.Serialize(recipe, RecipeJsonOptions);
        long id = conn.ExecuteScalar<long>(Sql.InsertNetwork, new { name, recipeJson });
        _logger.LogInformation("Saved network '{Name}' as id {Id}", name, id);
        return id;
    }

    public List<NetworkSummary> ListNetworks()
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        EnsureTable(conn);

        var result = conn.Query<NetworkRow>(Sql.SelectNetworkRows)
            .Select(r => new NetworkSummary(
                r.Id, r.Name, JsonSerializer.Deserialize<BrainRecipe>(r.RecipeJson, RecipeJsonOptions) ?? new BrainRecipe([], ArchitectureKind.Mlp, []),
                r.SavedAt, r.UpdatedAt))
            .ToList();
        _logger.LogInformation("Listed {NetworkCount} networks", result.Count);
        return result;
    }

    public void UpdateNetwork(long id, string name, BrainRecipe recipe)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        EnsureTable(conn);

        string recipeJson = JsonSerializer.Serialize(recipe, RecipeJsonOptions);
        conn.Execute(Sql.UpdateNetwork, new { id, name, recipeJson });
        _logger.LogInformation("Updated network id {Id} ('{Name}')", id, name);
    }

    public void DeleteNetwork(long id)
    {
        EnsureDatabaseExists();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        EnsureTable(conn);
        conn.Execute(Sql.DeleteNetwork, new { id });
        _logger.LogInformation("Deleted network id {Id}", id);
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

    private void EnsureTable(NpgsqlConnection conn) => conn.Execute(Sql.CreateTable);

    // Parameter order must match SelectNetworkRows' column order exactly --
    // Dapper's record-constructor materialization matches positionally, not
    // by name, same caveat as BrainRepository.BrainRow.
    private sealed record NetworkRow(long Id, string Name, string RecipeJson, DateTime SavedAt, DateTime UpdatedAt);

    // ── SQL ──
    private static class Sql
    {
        public const string DatabaseExists = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";

        // DatabaseName/TableName are compile-time constants, never user input,
        // so interpolating them as identifiers here is safe (same reasoning as
        // BrainRepository.Sql.CreateDatabase).
        public const string CreateDatabase = $"CREATE DATABASE \"{DatabaseName}\"";

        public const string CreateTable =
            $@"CREATE TABLE IF NOT EXISTS ""{TableName}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""name"" TEXT NOT NULL,
                   ""recipe"" JSONB NOT NULL,
                   ""saved_at"" TIMESTAMPTZ NOT NULL DEFAULT now(),
                   ""updated_at"" TIMESTAMPTZ NOT NULL DEFAULT now()
               );";

        public const string InsertNetwork =
            $@"INSERT INTO ""{TableName}"" (""name"", ""recipe"", ""saved_at"", ""updated_at"")
               VALUES (@name, @recipeJson::jsonb, now(), now())
               RETURNING ""id""";

        public const string SelectNetworkRows =
            $@"SELECT ""id"" AS ""Id"", ""name"" AS ""Name"", ""recipe""::text AS ""RecipeJson"",
                      ""saved_at"" AS ""SavedAt"", ""updated_at"" AS ""UpdatedAt""
               FROM ""{TableName}"" ORDER BY ""name""";

        public const string UpdateNetwork =
            $@"UPDATE ""{TableName}"" SET ""name"" = @name, ""recipe"" = @recipeJson::jsonb, ""updated_at"" = now()
               WHERE ""id"" = @id";

        public const string DeleteNetwork = $@"DELETE FROM ""{TableName}"" WHERE ""id"" = @id";
    }
}
