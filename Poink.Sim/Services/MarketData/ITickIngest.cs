using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>
	/// Write side of the tick store, kept separate from the read-only <see cref="ITickRepository"/>.
	/// The backfill worker is the single writer; it creates tables on explicit opt-in, appends ticks in
	/// bulk, and keeps the tick_table_stats registry current so coverage checks stay cheap.
	/// </summary>
	internal interface ITickIngest
	{
		bool TableExists(string table);

		/// <summary>Resolves the table registered for a symbol code, or null when none is mapped yet.</summary>
		string? GetTableForSymbol(string symbolCode);

		void CreateTable(string table);

		/// <summary>Records the symbol code against a table in the registry, creating the row (with computed stats) if absent.</summary>
		void AssociateSymbol(string table, string symbolCode);

		TickCoverage GetCoverage(string table);

		/// <summary>Returns every tick_table_stats row for inspection on the Data page.</summary>
		IReadOnlyList<TickTableStatRow> GetRegistry();

		/// <summary>Reads the saved import source directory for a table's registry row, or null if none is saved.</summary>
		string? GetSourcePath(string table);

		/// <summary>Records the filesystem path a table's ticks were imported from, so a later diff can check it for new files.</summary>
		void SetSourcePath(string table, string sourcePath);

		/// <summary>Bulk-appends the ticks (assumed sorted ascending by timestamp) and updates the registry. Returns the row count written.</summary>
		long InsertTicks(string table, IReadOnlyList<Tick> ticks);

		/// <summary>
		/// Atomically deletes the table's ticks in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>) and inserts
		/// <paramref name="ticks"/> (sorted ascending, inside that range) in their place. Returns the rows deleted and inserted.
		/// </summary>
		(long Deleted, long Inserted) ReplaceTicks(string table, DateTime fromUtc, DateTime toUtc, IReadOnlyList<Tick> ticks);

		/// <summary>Records a completed backfill fetch-and-store to the tick_backfill_log audit table.</summary>
		void LogBackfill(
			string table,
			long symbolCode,
			string symbolName,
			DateTime backfillFromUtc,
			DateTime backfillToUtc,
			long recordCount,
			BackfillTrigger trigger
		);

		/// <summary>Returns every tick_backfill_log row, most recent first.</summary>
		IReadOnlyList<BackfillLogEntry> GetBackfillLog();

		/// <summary>Upserts one calendar day's row count and time bounds into the tick_daily_stats registry.</summary>
		void UpsertDailyStat(string table, DateOnly day, long rowCount, DateTime? minUtc, DateTime? maxUtc);

		/// <summary>Recounts one UTC day straight from the tick table and upserts it into tick_daily_stats.</summary>
		void RefreshDailyStat(string table, DateOnly day);

		/// <summary>Returns every tick_daily_stats row for a table, ordered by day ascending.</summary>
		IReadOnlyList<TickDailyStatRow> GetDailyStats(string table);
	}
}
