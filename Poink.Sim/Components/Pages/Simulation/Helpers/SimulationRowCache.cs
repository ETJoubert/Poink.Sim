using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// Each symbol's chart and tick-table rows, rebuilt only when the engine publishes that symbol's bars or ticks
	/// anew, so an unchanged symbol costs no conversion per snapshot and keeps handing out the same list.
	/// </summary>
	internal sealed class SimulationRowCache
	{
		private sealed class Entry
		{
			public IReadOnlyList<HlocAvdtBar>? BarsSource;
			public List<CTraderBarRow> Bars = new();
			public IReadOnlyList<Tick>? TicksSource;
			public List<object> TickRows = new();
		}

		private readonly Dictionary<string, Entry> _entries = new();

		public List<CTraderBarRow> BarRows(string table, IReadOnlyList<HlocAvdtBar> bars)
		{
			var entry = EntryFor(table);
			if (!ReferenceEquals(entry.BarsSource, bars))
			{
				entry.BarsSource = bars;
				entry.Bars = SimulationRowHelper.ToBarRows(bars);
			}
			return entry.Bars;
		}

		public List<object> TickRows(string table, IReadOnlyList<Tick> ticks)
		{
			var entry = EntryFor(table);
			if (!ReferenceEquals(entry.TicksSource, ticks))
			{
				entry.TicksSource = ticks;
				entry.TickRows = SimulationRowHelper.ToTickRows(ticks);
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
