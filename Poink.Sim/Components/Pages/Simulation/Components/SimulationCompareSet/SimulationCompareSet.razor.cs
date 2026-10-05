using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation page's adapter onto the shared compare components: the traded symbol and every watched symbol on one
	/// <see cref="CompareChart"/> as % change by <see cref="Mode"/>, with an optional traded ÷ <see cref="RatioTable"/> line,
	/// plus optional <see cref="CompareTxFold"/> and <see cref="CompareXGrid"/> panels, any of which can take the main area.
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

		[Parameter]
		public SimulationCompareTxFoldSettings TxFold { get; set; } = new();

		[Parameter]
		public EventCallback<SimulationCompareTxFoldSettings> TxFoldChanged { get; set; }

		/// <summary>Where every symbol's TxFold windows end: just past the traded symbol's last bar in the Lookback Window.</summary>
		[Parameter]
		public DateTime? TxFoldAnchor { get; set; }

		/// <summary>Start of the simulated range; TxFold windows reaching before it are drawn as partial.</summary>
		[Parameter]
		public DateTime RangeStart { get; set; }

		[Parameter]
		public SimulationCompareXGridSettings XGrid { get; set; } = new();

		[Parameter]
		public EventCallback<SimulationCompareXGridSettings> XGridChanged { get; set; }

		/// <summary>Computed by the page from every symbol's bars and histograms, since those types stay internal to it.</summary>
		[Parameter]
		public CompareXGridResult? XGridResult { get; set; }

		/// <summary>The panel picked for the main area; falls back to the % chart while the picked panel is hidden.</summary>
		[Parameter]
		public SimulationComparePanel MainPanel { get; set; }

		[Parameter]
		public EventCallback<SimulationComparePanel> MainPanelChanged { get; set; }

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

		private bool AnchorApplies => Mode == SimulationCompareMode.WindowAnchored || XGrid.Show;

		private string XGridNoDataMessage() =>
			WindowStart is null
				? "No bars in the Lookback Window yet -- the grid covers it, with rows as % from each symbol's price at the Anchor edge."
				: "No ticks inside the Lookback Window for any symbol.";

		private Task SetXGrid(SimulationCompareXGridSettings settings) => XGridChanged.InvokeAsync(settings);

		private SimulationComparePanel EffectiveMain =>
			MainPanel switch
			{
				SimulationComparePanel.TxFold when TxFold.Show => SimulationComparePanel.TxFold,
				SimulationComparePanel.XGrid when XGrid.Show => SimulationComparePanel.XGrid,
				_ => SimulationComparePanel.Chart,
			};

		private IEnumerable<SimulationComparePanel> VisiblePanels
		{
			get
			{
				yield return SimulationComparePanel.Chart;
				if (TxFold.Show)
					yield return SimulationComparePanel.TxFold;
				if (XGrid.Show)
					yield return SimulationComparePanel.XGrid;
			}
		}

		private List<SimulationComparePanel> SidePanels => VisiblePanels.Where(p => p != EffectiveMain).ToList();

		private bool IsMain(SimulationComparePanel panel) => panel == EffectiveMain;

		private string PanelClass(SimulationComparePanel panel) => IsMain(panel) ? "scmp-panel scmp-panel-main" : "scmp-panel scmp-panel-side";

		// Grid rows rather than moving markup, so swapping never re-mounts a chart or reloads TxFold's ticks.
		private string PanelStyle(SimulationComparePanel panel) =>
			IsMain(panel) ? "grid-row: 1 / -1" : $"grid-row: {SidePanels.IndexOf(panel) + 1}";

		private string? BodyStyle => SidePanels.Count > 0 ? $"grid-template-rows: repeat({SidePanels.Count}, minmax(0, 1fr))" : null;

		private Task SetMainPanel(SimulationComparePanel panel) => MainPanelChanged.InvokeAsync(panel);

		private List<CompareTxFoldSymbol> TxFoldSymbols() => Symbols.Select(s => new CompareTxFoldSymbol(s.Table, s.ColorVar)).ToList();

		private IReadOnlyCollection<int> TxFoldSeconds => TxFold.Seconds ?? [.. SnapshotTimeframeOptions.DefaultSelectedSeconds];

		private Task SetTxFold(SimulationCompareTxFoldSettings settings) => TxFoldChanged.InvokeAsync(settings);

		private const string TxFoldColorVar = "--color-highlight";

		private IEnumerable<SnapshotTimeframe> TxFoldTimeframes => SnapshotTimeframeOptions.All.Where(o => TxFoldSeconds.Contains(o.Seconds));

		/// <summary>The span the largest selected TxFold window covers, ending at its anchor.</summary>
		private IReadOnlyList<CompareChartZone> TxFoldZones() =>
			TxFold.Show && TxFoldAnchor is { } anchor && TxFoldTimeframes.LastOrDefault() is { Seconds: > 0 } largest
				? [new CompareChartZone(anchor.AddSeconds(-largest.Seconds), anchor, TxFoldColorVar, $"TXFOLD {largest.Label}")]
				: [];

		/// <summary>Where each smaller TxFold window starts; the largest one's start is already the zone's labelled edge.</summary>
		private IReadOnlyList<CompareChartMarker> TxFoldMarkers() =>
			TxFold.Show && TxFoldAnchor is { } anchor
				? TxFoldTimeframes.SkipLast(1).Select(o => new CompareChartMarker(anchor.AddSeconds(-o.Seconds), TxFoldColorVar, o.Label)).ToList()
				: [];

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
			(HasRatio
				? $"Every symbol's {ModeNote()}, with {SimulationCompareHelper.RatioLabel(Traded!.Table, RatioTable!)} in the lower pane"
				: $"Every symbol's {ModeNote()}")
			+ (TxFold.Show ? ". The highlighted zone and dashed lines mark where each TxFold window starts" : "");

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
