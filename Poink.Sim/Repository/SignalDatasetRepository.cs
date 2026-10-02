using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

internal sealed class SignalDatasetRepository : ISignalDatasetRepository
{
	const string DatabaseName = "Poink.Sim";
	const string DatasetTableName = "signal_datasets";
	const string PointTableName = "signal_points";

	private readonly ILogger<SignalDatasetRepository> _logger;
	private readonly string _maintenanceConnectionString;
	private readonly string _connectionString;
	private bool _schemaReady;

	public SignalDatasetRepository(ILogger<SignalDatasetRepository> logger, IConfiguration configuration)
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

	public long CreateDataset(SignalDatasetBuildRequest request)
	{
		using var conn = OpenWithSchema();
		var id = conn.ExecuteScalar<long>(
			Sql.InsertDataset,
			new
			{
				name = request.Name,
				tableName = request.Table,
				fromUtc = DateTime.SpecifyKind(request.FromUtc, DateTimeKind.Utc),
				toUtc = DateTime.SpecifyKind(request.ToUtc, DateTimeKind.Utc),
				pipSize = request.PipSize,
				profileJson = JsonSerializer.Serialize(request.Profile),
				status = SignalDatasetStatus.Building.ToString(),
			}
		);
		_logger.LogInformation(
			"Created signal dataset {Id} '{Name}' for {Table} from {From} to {To}",
			id,
			request.Name,
			request.Table,
			request.FromUtc,
			request.ToUtc
		);
		return id;
	}

	public void AppendPoints(long datasetId, IReadOnlyList<SignalPoint> points)
	{
		if (points.Count == 0)
			return;

		using var conn = OpenWithSchema();
		using (var writer = conn.BeginBinaryImport(Sql.CopyPoints))
		{
			foreach (var point in points)
			{
				writer.StartRow();
				writer.Write(datasetId, NpgsqlDbType.Bigint);
				writer.Write(DateTime.SpecifyKind(point.TimestampUtc, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
				writer.Write(point.Bid, NpgsqlDbType.Double);
				writer.Write(point.Ask, NpgsqlDbType.Double);
				if (point.Atr is { } atr)
					writer.Write(atr, NpgsqlDbType.Double);
				else
					writer.WriteNull();
				writer.Write(point.AboveMask, NpgsqlDbType.Bigint);
				writer.Write(point.InMask, NpgsqlDbType.Bigint);
				writer.Write(point.BelowMask, NpgsqlDbType.Bigint);
			}
			writer.Complete();
		}
		_logger.LogDebug("Appended {Count} points to signal dataset {Id}", points.Count, datasetId);
	}

	public void FinishDataset(long datasetId, SignalDatasetStatus status, IReadOnlyList<SignalCatalogEntry> catalog, long pointCount)
	{
		using var conn = OpenWithSchema();
		conn.Execute(
			Sql.FinishDataset,
			new
			{
				id = datasetId,
				status = status.ToString(),
				catalogJson = JsonSerializer.Serialize(catalog),
				pointCount,
			}
		);
		_logger.LogInformation(
			"Signal dataset {Id} finished as {Status} with {PointCount} points and {SignalCount} signal rows",
			datasetId,
			status,
			pointCount,
			catalog.Count
		);
	}

	public IReadOnlyList<SignalDatasetInfo> ListDatasets()
	{
		using var conn = OpenWithSchema();
		var datasets = conn.Query<DatasetRow>(Sql.SelectDatasets).Select(ToInfo).ToList();
		_logger.LogInformation("Listed {Count} signal datasets", datasets.Count);
		return datasets;
	}

	public void DeleteDataset(long datasetId)
	{
		using var conn = OpenWithSchema();
		conn.Execute(Sql.DeleteDataset, new { id = datasetId });
		_logger.LogInformation("Deleted signal dataset {Id}", datasetId);
	}

	public IEnumerable<SignalPoint> ReadPoints(long datasetId)
	{
		using var conn = OpenWithSchema();
		_logger.LogInformation("Reading points of signal dataset {Id}", datasetId);
		foreach (var row in conn.Query<PointRow>(Sql.SelectPoints, new { id = datasetId }, buffered: false))
			yield return new SignalPoint(row.Ts, row.Bid, row.Ask, row.Atr, row.AboveMask, row.InMask, row.BelowMask);
	}

	private NpgsqlConnection OpenWithSchema()
	{
		if (!_schemaReady)
			EnsureDatabaseExists();
		var conn = new NpgsqlConnection(_connectionString);
		conn.Open();
		if (!_schemaReady)
		{
			conn.Execute(Sql.CreateDatasetTable);
			conn.Execute(Sql.CreatePointTable);
			conn.Execute(Sql.CreatePointIndex);
			_schemaReady = true;
		}
		return conn;
	}

	private void EnsureDatabaseExists()
	{
		using var conn = new NpgsqlConnection(_maintenanceConnectionString);
		conn.Open();
		if (conn.ExecuteScalar<bool>(Sql.DatabaseExists, new { name = DatabaseName }))
			return;
		_logger.LogInformation("Database {DatabaseName} does not exist; creating it", DatabaseName);
		conn.Execute(Sql.CreateDatabase);
	}

	private static SignalDatasetInfo ToInfo(DatasetRow row) =>
		new(
			row.Id,
			row.Name,
			row.TableName,
			row.FromUtc,
			row.ToUtc,
			row.PipSize,
			JsonSerializer.Deserialize<SignalProfile>(row.ProfileJson) ?? new SignalProfile(),
			JsonSerializer.Deserialize<List<SignalCatalogEntry>>(row.CatalogJson) ?? new List<SignalCatalogEntry>(),
			row.PointCount,
			Enum.TryParse<SignalDatasetStatus>(row.Status, out var status) ? status : SignalDatasetStatus.Failed,
			row.CreatedAtUtc,
			row.CompletedAtUtc
		);

	private sealed record DatasetRow(
		long Id,
		string Name,
		string TableName,
		DateTime FromUtc,
		DateTime ToUtc,
		double PipSize,
		string ProfileJson,
		string CatalogJson,
		long PointCount,
		string Status,
		DateTime CreatedAtUtc,
		DateTime? CompletedAtUtc
	);

	private sealed record PointRow(DateTime Ts, double Bid, double Ask, double? Atr, long AboveMask, long InMask, long BelowMask);

	// ── SQL ──
	private static class Sql
	{
		public const string DatabaseExists = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";

		public const string CreateDatabase = $"CREATE DATABASE \"{DatabaseName}\"";

		public const string CreateDatasetTable =
			$@"CREATE TABLE IF NOT EXISTS ""{DatasetTableName}"" (
                   ""id"" BIGSERIAL PRIMARY KEY,
                   ""name"" TEXT NOT NULL,
                   ""table_name"" TEXT NOT NULL,
                   ""from_utc"" TIMESTAMPTZ NOT NULL,
                   ""to_utc"" TIMESTAMPTZ NOT NULL,
                   ""pip_size"" DOUBLE PRECISION NOT NULL,
                   ""profile"" JSONB NOT NULL,
                   ""catalog"" JSONB NOT NULL DEFAULT '[]'::jsonb,
                   ""point_count"" BIGINT NOT NULL DEFAULT 0,
                   ""status"" TEXT NOT NULL,
                   ""created_at"" TIMESTAMPTZ NOT NULL DEFAULT now(),
                   ""completed_at"" TIMESTAMPTZ NULL
               );";

		public const string CreatePointTable =
			$@"CREATE TABLE IF NOT EXISTS ""{PointTableName}"" (
                   ""dataset_id"" BIGINT NOT NULL REFERENCES ""{DatasetTableName}""(""id"") ON DELETE CASCADE,
                   ""ts"" TIMESTAMPTZ NOT NULL,
                   ""bid"" DOUBLE PRECISION NOT NULL,
                   ""ask"" DOUBLE PRECISION NOT NULL,
                   ""atr"" DOUBLE PRECISION NULL,
                   ""above_mask"" BIGINT NOT NULL,
                   ""in_mask"" BIGINT NOT NULL,
                   ""below_mask"" BIGINT NOT NULL
               );";

		public const string CreatePointIndex =
			$@"CREATE INDEX IF NOT EXISTS ""idx_{PointTableName}_dataset_ts"" ON ""{PointTableName}"" (""dataset_id"", ""ts"")";

		public const string InsertDataset =
			$@"INSERT INTO ""{DatasetTableName}"" (""name"", ""table_name"", ""from_utc"", ""to_utc"", ""pip_size"", ""profile"", ""status"")
               VALUES (@name, @tableName, @fromUtc, @toUtc, @pipSize, @profileJson::jsonb, @status)
               RETURNING ""id""";

		public const string CopyPoints =
			$@"COPY ""{PointTableName}"" (""dataset_id"", ""ts"", ""bid"", ""ask"", ""atr"", ""above_mask"", ""in_mask"", ""below_mask"") FROM STDIN (FORMAT BINARY)";

		public const string FinishDataset =
			$@"UPDATE ""{DatasetTableName}""
               SET ""status"" = @status, ""catalog"" = @catalogJson::jsonb, ""point_count"" = @pointCount, ""completed_at"" = now()
               WHERE ""id"" = @id";

		public const string SelectDatasets =
			$@"SELECT ""id"" AS ""Id"", ""name"" AS ""Name"", ""table_name"" AS ""TableName"", ""from_utc"" AS ""FromUtc"", ""to_utc"" AS ""ToUtc"",
                      ""pip_size"" AS ""PipSize"", ""profile""::text AS ""ProfileJson"", ""catalog""::text AS ""CatalogJson"",
                      ""point_count"" AS ""PointCount"", ""status"" AS ""Status"", ""created_at"" AS ""CreatedAtUtc"", ""completed_at"" AS ""CompletedAtUtc""
               FROM ""{DatasetTableName}"" ORDER BY ""created_at"" DESC";

		public const string DeleteDataset = $@"DELETE FROM ""{DatasetTableName}"" WHERE ""id"" = @id";

		public const string SelectPoints =
			$@"SELECT ""ts"" AS ""Ts"", ""bid"" AS ""Bid"", ""ask"" AS ""Ask"", ""atr"" AS ""Atr"",
                      ""above_mask"" AS ""AboveMask"", ""in_mask"" AS ""InMask"", ""below_mask"" AS ""BelowMask""
               FROM ""{PointTableName}"" WHERE ""dataset_id"" = @id ORDER BY ""ts""";
	}
}
