using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Feeds the Trade compare TxFold tick by tick: each symbol's held series supplies every tick from its first one on
	/// (loaded history plus live ticks not yet written), and only the older part of a window is read from the tick tables
	/// -- once, then cached, since the window's start only moves forward as the anchor follows the latest tick.
	/// </summary>
	internal sealed class TradeLiveTickSource : ICompareTxFoldTickSource
	{
		private readonly ITickRepository _repository;
		private readonly ITradeBarFeed _feed;
		private readonly ConcurrentDictionary<string, StoredBlock> _stored = new();

		public TradeLiveTickSource(ITickRepository repository, ITradeBarFeed feed)
		{
			_repository = repository;
			_feed = feed;
		}

		public IReadOnlyList<Tick> ReadTicks(CompareTxFoldSymbol symbol, DateTime? historyBeforeUtc, DateTime fromUtc, DateTime toUtc)
		{
			var series = _feed.GetByTable(symbol.Table);
			var heldFrom = series?.FirstTickUtc ?? toUtc;

			var ticks = new List<Tick>();
			if (fromUtc < heldFrom)
				ticks.AddRange(Slice(StoredBefore(symbol, historyBeforeUtc, fromUtc, heldFrom), fromUtc, toUtc));
			if (series is not null && toUtc > heldFrom)
				ticks.AddRange(series.TicksBetween(fromUtc > heldFrom ? fromUtc : heldFrom, toUtc));
			return ticks;
		}

		// Reused while it still reaches back to fromUtc and ends where the held ticks begin; a series reload moves that edge.
		private List<Tick> StoredBefore(CompareTxFoldSymbol symbol, DateTime? historyBeforeUtc, DateTime fromUtc, DateTime heldFromUtc)
		{
			if (_stored.TryGetValue(symbol.Table, out var block) && block.FromUtc <= fromUtc && block.ToUtc == heldFromUtc)
				return block.Ticks;

			var ticks = SplitTickReadHelper.Read(_repository, symbol.Table, symbol.HistoryTable, historyBeforeUtc, fromUtc, heldFromUtc);
			_stored[symbol.Table] = new StoredBlock(fromUtc, heldFromUtc, ticks);
			return ticks;
		}

		private static IEnumerable<Tick> Slice(List<Tick> sorted, DateTime fromUtc, DateTime toUtc)
		{
			var start = LowerBound(sorted, fromUtc);
			var end = LowerBound(sorted, toUtc);
			return end > start ? sorted.GetRange(start, end - start) : Enumerable.Empty<Tick>();
		}

		private static int LowerBound(List<Tick> sorted, DateTime timeUtc)
		{
			int low = 0, high = sorted.Count;
			while (low < high)
			{
				var mid = (low + high) / 2;
				if (sorted[mid].Timestamp < timeUtc)
					low = mid + 1;
				else
					high = mid;
			}
			return low;
		}

		private sealed record StoredBlock(DateTime FromUtc, DateTime ToUtc, List<Tick> Ticks);
	}
}
