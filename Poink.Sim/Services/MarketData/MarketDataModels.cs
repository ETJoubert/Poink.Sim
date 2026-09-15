using System;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>A single merged bid/ask quote for a symbol, carrying cTrader's server timestamp when available.</summary>
	public readonly record struct MarketTick(
		long SymbolId,
		string SymbolName,
		DateTime TimestampUtc,
		double Bid,
		double Ask
	);

	/// <summary>Per-table coverage as tracked by the tick_table_stats registry.</summary>
	public readonly record struct TickCoverage(bool TableExists, long Rows, DateTime? MinUtc, DateTime? MaxUtc);

	public enum TickIngestState
	{
		NoTable,
		Idle,
		ClosingGap,
		DrainingBuffer,
		TailingLive,
		Error,
	}

	/// <summary>One row of the tick_table_stats registry, shown on the Data page for inspection.</summary>
	public sealed record TickTableStatRow(
		string Table,
		string? Symbol,
		long Rows,
		DateTime? MinUtc,
		DateTime? MaxUtc,
		DateTime? RefreshedAt
	);

	/// <summary>Who caused a backfill: an explicit UI action, or the worker's own gap-closing.</summary>
	public enum BackfillTrigger
	{
		User,
		Worker,
	}

	/// <summary>One row of the tick_backfill_log audit table: a completed backfill fetch-and-store.</summary>
	public sealed record BackfillLogEntry(
		long Id,
		string TickTableName,
		long SymbolCode,
		string SymbolName,
		DateTime BackfillFrom,
		DateTime BackfillTo,
		long RecordCount,
		BackfillTrigger TriggerEntity,
		DateTime TriggeredAtUtc
	);

	/// <summary>One calendar day's row count and time bounds for a tick table, used to spot gaps without scanning the table itself.</summary>
	public sealed record TickDailyStatRow(
		string Table,
		DateOnly Day,
		long RowCount,
		DateTime? MinUtc,
		DateTime? MaxUtc,
		DateTime RefreshedAt
	);

	public sealed record SymbolIngestStatus(
		long SymbolId,
		string SymbolName,
		string Table,
		bool TableExists,
		TickIngestState State,
		long RowsInDb,
		DateTime? MinUtc,
		DateTime? MaxUtc,
		int Buffered,
		long WrittenThisSession,
		double? LagSeconds,
		string? LastError
	);
}
