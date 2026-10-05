using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Profiler;
using Poink.Sim.Components.Simulation;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// The watch-only symbols replayed alongside the traded one: their pip sizes are resolved before a load, since
	/// each symbol's price buckets are sized from its own pip, and their live charts are shown under the traded symbol's.
	/// </summary>
	public partial class SimulationPage
	{
		/// <summary>Used when a watched table maps to no known symbol, matching the Profiler's fallback.</summary>
		private const double WatchFallbackPipSize = 0.01;

		private List<string> _watchTables = new();
		private readonly Dictionary<string, double> _watchPipSizes = new();
		private bool _showWatchedCharts = true;
		private readonly SimulationRowCache _rowCache = new();
		private SimulationSymbolPanelBuilder? _panelBuilder;
		private readonly Dictionary<string, int> _watchXGridPipCounts = new();

		private SimulationSymbolPanelBuilder PanelBuilder => _panelBuilder ??= new(_rowCache);

		private int WatchXGridPipCount(string table) => _watchXGridPipCounts.GetValueOrDefault(table, 20);

		private void SetWatchXGridPipCount(string table, int count) => _watchXGridPipCounts[table] = count;

		private SimulationChartSetOptions ChartSetOptions(SimulationSnapshot s) =>
			new(
				s.Config.Aggregate,
				_showMainChart,
				_showXGrid,
				_showVolumeProfile,
				_showTimeProfile,
				_showSnapshot,
				_indicators,
				s.Config.From
			);

		private List<SimulationSymbolPanelData> WatchedPanels(SimulationSnapshot s)
		{
			var colors = SymbolColors(s);
			var window = LookbackWindowTimes(s);
			var pipSizes = (s.Config.WatchSymbols ?? []).ToDictionary(w => w.Table, w => w.PipSize);
			return WatchedSymbols(s)
				.Select(w => PanelBuilder.Build(
					w,
					ProfilerSymbolColorHelper.ColorVarFor(colors, w.Table),
					pipSizes.GetValueOrDefault(w.Table, WatchFallbackPipSize),
					s.Config.Aggregate,
					window,
					_indicators,
					_showXGrid,
					WatchXGridPipCount(w.Table),
					_xgridColCount,
					_showTimeProfile
				))
				.ToList();
		}

		private static IReadOnlyList<WatchedSymbolSnapshot> WatchedSymbols(SimulationSnapshot s) => s.WatchedSymbols ?? [];

		private IReadOnlyList<string>? LoadedTables =>
			_snapshot is null ? null : [_snapshot.Config.Table, .. WatchedSymbols(_snapshot).Select(w => w.Table)];

		private async Task<IReadOnlyList<SimulationWatchSymbol>> ResolveWatchSymbolsAsync()
		{
			var symbols = new List<SimulationWatchSymbol>();
			foreach (var table in _watchTables)
				symbols.Add(new SimulationWatchSymbol(table, await ResolveWatchPipSizeAsync(table)));
			return symbols;
		}

		private async Task<double> ResolveWatchPipSizeAsync(string table)
		{
			if (_watchPipSizes.TryGetValue(table, out var cached))
				return cached;

			var pipSize = WatchFallbackPipSize;
			try
			{
				var resolution = await SymbolTableResolver.ResolveAsync(table);
				if (resolution.Limits is { } limits)
				{
					pipSize = limits.PipSize;
					_watchPipSizes[table] = pipSize;
					Logger.LogInformation("Simulation watched symbol {Table} pip size resolved to {PipSize}", table, pipSize);
				}
				else
					Logger.LogWarning(
						"No trading metadata for watched symbol {Table}; Simulation assumes a {PipSize} pip size",
						table,
						pipSize
					);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(
					ex,
					"Could not resolve the pip size for watched symbol {Table}; Simulation assumes a {PipSize} pip size",
					table,
					pipSize
				);
			}
			return pipSize;
		}
	}
}
