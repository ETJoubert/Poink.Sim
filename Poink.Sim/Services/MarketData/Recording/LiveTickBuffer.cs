using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData.Recording
{
	/// <summary>One table's drained live ticks, sorted ascending with one quote per timestamp (the latest received).</summary>
	internal sealed record LiveTickBatch(string Table, long SymbolId, string SymbolName, IReadOnlyList<Tick> Ticks);

	/// <summary>A buffered table awaiting a write, with its oldest tick -- the point today's API catch-up must reach.</summary>
	internal readonly record struct PendingLiveTable(string Table, long SymbolId, string SymbolName, DateTime EarliestUtc);

	/// <summary>Thread-safe per-table accumulator between the tick stream (writer) and the recorder's flush loop (reader).</summary>
	internal sealed class LiveTickBuffer
	{
		private readonly Lock _lock = new();
		private readonly Dictionary<string, PendingTicks> _pending = new();

		public int Count
		{
			get
			{
				lock (_lock)
					return _pending.Values.Sum(p => p.Ticks.Count);
			}
		}

		public void Add(string table, long symbolId, string symbolName, Tick tick)
		{
			lock (_lock)
			{
				if (!_pending.TryGetValue(table, out var pending))
				{
					pending = new PendingTicks(symbolId, symbolName);
					_pending[table] = pending;
				}
				pending.Ticks.Add(tick);
			}
		}

		public IReadOnlyList<PendingLiveTable> PendingTables()
		{
			lock (_lock)
				return _pending
					.Select(kv => new PendingLiveTable(kv.Key, kv.Value.SymbolId, kv.Value.SymbolName, kv.Value.Ticks.Min(t => t.Timestamp)))
					.ToList();
		}

		public LiveTickBatch? Drain(string table)
		{
			PendingTicks? pending;
			lock (_lock)
			{
				if (!_pending.Remove(table, out pending))
					return null;
			}

			return new LiveTickBatch(table, pending.SymbolId, pending.SymbolName, LatestPerTimestamp(pending.Ticks));
		}

		/// <summary>Drops a table's ticks unwritten; returns how many were dropped.</summary>
		public int Discard(string table)
		{
			lock (_lock)
				return _pending.Remove(table, out var pending) ? pending.Ticks.Count : 0;
		}

		private static List<Tick> LatestPerTimestamp(List<Tick> ticks)
		{
			var ordered = ticks.Select((tick, index) => (tick, index)).OrderBy(x => x.tick.Timestamp).ThenBy(x => x.index).ToList();
			var result = new List<Tick>(ordered.Count);
			foreach (var (tick, _) in ordered)
			{
				if (result.Count > 0 && result[^1].Timestamp == tick.Timestamp)
					result[^1] = tick;
				else
					result.Add(tick);
			}
			return result;
		}

		private sealed class PendingTicks(long symbolId, string symbolName)
		{
			public long SymbolId { get; } = symbolId;

			public string SymbolName { get; } = symbolName;

			public List<Tick> Ticks { get; } = new();
		}
	}
}
