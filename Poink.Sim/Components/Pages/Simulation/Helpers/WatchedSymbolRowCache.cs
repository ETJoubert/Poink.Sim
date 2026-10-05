using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// Each watched symbol's chart and tick-table rows, rebuilt only when the engine publishes that symbol's bars or
	/// ticks anew, so an unchanged symbol costs no conversion per snapshot.
	/// </summary>
	internal sealed class WatchedSymbolRowCache
	{
		private sealed class Entry
		{
			public IReadOnlyList<HlocAvdtBar>? BarsSource;
			public List<CTraderBarRow> Bars = new();
			public IReadOnlyList<Tick>? TicksSource;
			public List<object> TickRows = new();
		}

		private readonly Dictionary<string, Entry> _entries = new();

		public List<CTraderBarRow> BarRows(WatchedSymbolSnapshot symbol)
		{
			var entry = EntryFor(symbol.Table);
			if (!ReferenceEquals(entry.BarsSource, symbol.RecentBars))
			{
				entry.BarsSource = symbol.RecentBars;
				entry.Bars = SimulationRowHelper.ToBarRows(symbol.RecentBars);
			}
			return entry.Bars;
		}

		public List<object> TickRows(WatchedSymbolSnapshot symbol)
		{
			var entry = EntryFor(symbol.Table);
			if (!ReferenceEquals(entry.TicksSource, symbol.RecentTicks))
			{
				entry.TicksSource = symbol.RecentTicks;
				entry.TickRows = SimulationRowHelper.ToTickRows(symbol.RecentTicks);
			}
			return entry.TickRows;
		}

		private Entry EntryFor(string table)
		{
			if (!_entries.TryGetValue(table, out var entry))
				_entries[table] = entry = new Entry();
			return entry;
		}
	}
}
