using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Tests.TestSupport;

/// <summary>Read and write sides of the tick store over one in-memory table map, deduplicating on timestamp like the real unique index.</summary>
internal sealed class InMemoryTickRepository : ITickRepository, ITickIngest
{
	private readonly Dictionary<string, SortedDictionary<DateTime, Tick>> _tables = new();

	public List<(string Table, DateOnly Day)> RefreshedDays { get; } = new();

	public List<(string Table, DateTime From, DateTime To, long Count, BackfillTrigger Trigger)> BackfillLog { get; } = new();

	public void Seed(string table, IEnumerable<Tick> ticks)
	{
		CreateTable(table);
		InsertTicks(table, ticks.ToList());
	}

	public IReadOnlyList<Tick> All(string table) => _tables.TryGetValue(table, out var rows) ? rows.Values.ToList() : Array.Empty<Tick>();

	public bool TableExists(string table) => _tables.ContainsKey(table);

	public void CreateTable(string table) => _tables.TryAdd(table, new SortedDictionary<DateTime, Tick>());

	public void AssociateSymbol(string table, string symbolCode) { }

	public long InsertTicks(string table, IReadOnlyList<Tick> ticks)
	{
		var rows = _tables[table];
		return ticks.Count(t => rows.TryAdd(t.Timestamp, t));
	}

	public void RefreshDailyStat(string table, DateOnly day) => RefreshedDays.Add((table, day));

	public void LogBackfill(
		string table,
		long symbolCode,
		string symbolName,
		DateTime backfillFromUtc,
		DateTime backfillToUtc,
		long recordCount,
		BackfillTrigger trigger
	) => BackfillLog.Add((table, backfillFromUtc, backfillToUtc, recordCount, trigger));

	public IEnumerable<Tick> ReadTicks(string table, DateTime? from = null, DateTime? to = null) =>
		All(table).Where(t => (from is null || t.Timestamp >= from) && (to is null || t.Timestamp < to));

	public List<Tick> ReadRecentTicks(string table, DateTime beforeUtc, int count) =>
		All(table).Where(t => t.Timestamp <= beforeUtc).TakeLast(count).ToList();

	public bool HasCachedTables => throw new NotSupportedException();

	public IReadOnlyList<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> CachedTables => throw new NotSupportedException();

	public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> ListTickTables() => throw new NotSupportedException();

	public List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> RefreshTickTableStats() => throw new NotSupportedException();

	public List<Tick> ReadWindow(string table, DateTime start, int count) => throw new NotSupportedException();

	public long CountTicks(string table, DateTime from, DateTime to) => throw new NotSupportedException();

	public string? GetTableForSymbol(string symbolCode) => throw new NotSupportedException();

	public TickCoverage GetCoverage(string table) => throw new NotSupportedException();

	public IReadOnlyList<TickTableStatRow> GetRegistry() => throw new NotSupportedException();

	public string? GetSourcePath(string table) => throw new NotSupportedException();

	public void SetSourcePath(string table, string sourcePath) => throw new NotSupportedException();

	public IReadOnlyList<BackfillLogEntry> GetBackfillLog() => throw new NotSupportedException();

	public void UpsertDailyStat(string table, DateOnly day, long rowCount, DateTime? minUtc, DateTime? maxUtc) =>
		throw new NotSupportedException();

	public IReadOnlyList<TickDailyStatRow> GetDailyStats(string table) => throw new NotSupportedException();
}

/// <summary>Serves API history from a fixed tick list and records every requested range.</summary>
internal sealed class FakeCTraderHistory : ICTraderHistory
{
	public bool CanFetchHistory { get; set; } = true;

	public bool UseLiveEnvironment { get; set; } = true;

	public List<Tick> Available { get; } = new();

	public List<(DateTime From, DateTime To)> Requests { get; } = new();

	public Task<IReadOnlyList<Tick>> GetHistoricalTicksAsync(
		long symbolId,
		DateTime fromUtc,
		DateTime toUtc,
		CancellationToken cancellationToken = default
	)
	{
		Requests.Add((fromUtc, toUtc));
		IReadOnlyList<Tick> ticks = Available.Where(t => t.Timestamp >= fromUtc && t.Timestamp < toUtc).ToList();
		return Task.FromResult(ticks);
	}
}
