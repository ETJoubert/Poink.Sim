using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation page's adapter onto the shared <see cref="CompareChart"/>: the traded symbol and every watched
	/// symbol as % change by <see cref="Mode"/>, with an optional traded ÷ <see cref="RatioTable"/> line in a lower pane.
	/// </summary>
	public partial class SimulationCompareSet : ComponentBase
	{
		/// <summary>Traded symbol first; a new list is expected only when some symbol's bars changed.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<SimulationCompareSymbol> Symbols { get; set; } = [];

		[Parameter]
		public SimulationCompareMode Mode { get; set; }

		[Parameter]
		public EventCallback<SimulationCompareMode> ModeChanged { get; set; }

		[Parameter]
		public SimulationCompareAnchor Anchor { get; set; }

		[Parameter]
		public EventCallback<SimulationCompareAnchor> AnchorChanged { get; set; }

		[Parameter]
		public CompareChartSeriesStyle Style { get; set; }

		[Parameter]
		public EventCallback<CompareChartSeriesStyle> StyleChanged { get; set; }

		/// <summary>The watched symbol the traded one is divided by for the ratio line; null or not watched hides it.</summary>
		[Parameter]
		public string? RatioTable { get; set; }

		[Parameter]
		public EventCallback<string?> RatioTableChanged { get; set; }

		/// <summary>The Lookback Window's left edge, shaded on the chart and the Start anchor.</summary>
		[Parameter]
		public DateTime? WindowStart { get; set; }

		/// <summary>The Lookback Window's right edge, shaded on the chart and the End anchor.</summary>
		[Parameter]
		public DateTime? WindowEnd { get; set; }

		private List<CompareChartSeries> _series = new();
		private (
			IReadOnlyList<SimulationCompareSymbol> Symbols,
			SimulationCompareMode Mode,
			DateTime? Anchor,
			string? RatioTable,
			CompareChartSeriesStyle Style
		)? _builtFor;

		private SimulationCompareSymbol? Traded => Symbols.Count > 0 ? Symbols[0] : null;

		private bool HasRatio => Symbols.Skip(1).Any(s => s.Table == RatioTable);

		// Without a window, End falls back to the traded symbol's latest bar the way Start falls back to each first bar.
		private DateTime? AnchorTime =>
			Mode != SimulationCompareMode.WindowAnchored ? null
			: Anchor == SimulationCompareAnchor.WindowStart ? WindowStart
			: WindowEnd ?? (Traded is { Bars.Count: > 0 } traded ? traded.Bars[^1].TimeUtc : null);

		private CompareChartScaleMode ScaleMode =>
			Mode == SimulationCompareMode.WindowAnchored ? CompareChartScaleMode.PercentValues : CompareChartScaleMode.Rebased;

		protected override void OnParametersSet()
		{
			var key = (Symbols, Mode, AnchorTime, RatioTable, Style);
			if (_builtFor == key)
				return;

			_builtFor = key;
			_series = SimulationCompareHelper.Build(Symbols, Mode, AnchorTime, RatioTable, Style);
		}

		private string ModeNote() =>
			(Mode, Anchor) switch
			{
				(SimulationCompareMode.PercentChange, _) => "% change from the first bar in view",
				(_, SimulationCompareAnchor.WindowStart) when WindowStart is { } start => $"% change from the window start, {start:yyyy-MM-dd HH:mm}",
				(_, SimulationCompareAnchor.WindowStart) => "% change from each symbol's first bar",
				_ when WindowEnd is { } end => $"% change relative to the window end, {end:yyyy-MM-dd HH:mm}",
				_ => "% change relative to the latest bar",
			};

		private string ChartTooltip() =>
			HasRatio
				? $"Every symbol's {ModeNote()}, with {SimulationCompareHelper.RatioLabel(Traded!.Table, RatioTable!)} in the lower pane"
				: $"Every symbol's {ModeNote()}";

		private Task OnModeSelected(ChangeEventArgs e) =>
			ModeChanged.InvokeAsync(Enum.TryParse<SimulationCompareMode>(e.Value?.ToString(), out var mode) ? mode : SimulationCompareMode.PercentChange);

		private Task OnAnchorSelected(ChangeEventArgs e) =>
			AnchorChanged.InvokeAsync(Enum.TryParse<SimulationCompareAnchor>(e.Value?.ToString(), out var anchor) ? anchor : SimulationCompareAnchor.WindowStart);

		private Task OnStyleSelected(ChangeEventArgs e) =>
			StyleChanged.InvokeAsync(Enum.TryParse<CompareChartSeriesStyle>(e.Value?.ToString(), out var style) ? style : CompareChartSeriesStyle.Lines);

		private Task OnRatioSelected(ChangeEventArgs e)
		{
			var table = e.Value?.ToString();
			return RatioTableChanged.InvokeAsync(string.IsNullOrEmpty(table) ? null : table);
		}
	}
}
