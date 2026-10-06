using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// A page's primary symbol and every other symbol on one <see cref="CompareChart"/> as % change by <see cref="Mode"/>,
	/// with an optional primary ÷ <see cref="RatioTable"/> line, plus optional <see cref="CompareTxFold"/> and
	/// <see cref="CompareXGrid"/> panels, any of which can take the main area. Shared by the Simulation and Trade pages.
	/// </summary>
	public partial class CompareSet : ComponentBase
	{
		/// <summary>Primary symbol first; a new list is expected only when some symbol's bars changed.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<CompareSetSymbol> Symbols { get; set; } = [];

		/// <summary>What the host calls its first symbol in labels and tooltips, e.g. "traded".</summary>
		[Parameter]
		public string PrimaryRole { get; set; } = "primary";

		/// <summary>What the host calls every other symbol, e.g. "watched".</summary>
		[Parameter]
		public string SecondaryRole { get; set; } = "watched";

		/// <summary>Tooltip on the COMPARE title, saying where the symbols' bars come from.</summary>
		[Parameter]
		public string Description { get; set; } = "Every symbol on one chart";

		/// <summary>Passed to <see cref="CompareTxFold"/>: ticks before it are read from each symbol's history table.</summary>
		[Parameter]
		public DateTime? HistoryBeforeUtc { get; set; }

		/// <summary>Passed to <see cref="CompareTxFold"/> to supply its ticks in place of the tick tables.</summary>
		[Parameter]
		public ICompareTxFoldTickSource? TxFoldTickSource { get; set; }

		[Parameter]
		public CompareSetMode Mode { get; set; }

		[Parameter]
		public EventCallback<CompareSetMode> ModeChanged { get; set; }

		[Parameter]
		public CompareSetAnchor Anchor { get; set; }

		[Parameter]
		public EventCallback<CompareSetAnchor> AnchorChanged { get; set; }

		[Parameter]
		public CompareChartSeriesStyle Style { get; set; }

		[Parameter]
		public EventCallback<CompareChartSeriesStyle> StyleChanged { get; set; }

		/// <summary>The secondary symbol the primary one is divided by for the ratio line; null or absent hides it.</summary>
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
		public CompareSetTxFoldSettings TxFold { get; set; } = new();

		[Parameter]
		public EventCallback<CompareSetTxFoldSettings> TxFoldChanged { get; set; }

		/// <summary>Where every symbol's TxFold windows end: just past the primary symbol's last bar in the Lookback Window.</summary>
		[Parameter]
		public DateTime? TxFoldAnchor { get; set; }

		/// <summary>Start of the host's data range; TxFold windows reaching before it are drawn as partial.</summary>
		[Parameter]
		public DateTime RangeStart { get; set; }

		[Parameter]
		public CompareSetXGridSettings XGrid { get; set; } = new();

		[Parameter]
		public EventCallback<CompareSetXGridSettings> XGridChanged { get; set; }

		/// <summary>Computed by the page from every symbol's bars and histograms, since those types stay internal to it.</summary>
		[Parameter]
		public CompareXGridResult? XGridResult { get; set; }

		/// <summary>The panel picked for the main area; falls back to the % chart while the picked panel is hidden.</summary>
		[Parameter]
		public CompareSetPanel MainPanel { get; set; }

		[Parameter]
		public EventCallback<CompareSetPanel> MainPanelChanged { get; set; }

		private List<CompareChartSeries> _series = new();
		private (
			IReadOnlyList<CompareSetSymbol> Symbols,
			CompareSetMode Mode,
			DateTime? Anchor,
			string? RatioTable,
			CompareChartSeriesStyle Style
		)? _builtFor;

		private CompareSetSymbol? Primary => Symbols.Count > 0 ? Symbols[0] : null;

		private bool HasRatio => Symbols.Skip(1).Any(s => s.Table == RatioTable);

		// Without a window, End falls back to the primary symbol's latest bar the way Start falls back to each first bar.
		private DateTime? AnchorTime =>
			Mode != CompareSetMode.WindowAnchored ? null
			: Anchor == CompareSetAnchor.WindowStart ? WindowStart
			: WindowEnd ?? (Primary is { Bars.Count: > 0 } primary ? primary.Bars[^1].TimeUtc : null);

		private CompareChartScaleMode ScaleMode =>
			Mode == CompareSetMode.WindowAnchored ? CompareChartScaleMode.PercentValues : CompareChartScaleMode.Rebased;

		protected override void OnParametersSet()
		{
			var key = (Symbols, Mode, AnchorTime, RatioTable, Style);
			if (_builtFor == key)
				return;

			_builtFor = key;
			_series = CompareSetHelper.Build(Symbols, Mode, AnchorTime, RatioTable, Style);
		}

		private bool AnchorApplies => Mode == CompareSetMode.WindowAnchored || XGrid.Show;

		private string XGridNoDataMessage() =>
			WindowStart is null
				? "No bars in the Lookback Window yet -- the grid covers it, with rows as % from each symbol's price at the Anchor edge."
				: "No ticks inside the Lookback Window for any symbol.";

		private Task SetXGrid(CompareSetXGridSettings settings) => XGridChanged.InvokeAsync(settings);

		private CompareSetPanel EffectiveMain =>
			MainPanel switch
			{
				CompareSetPanel.TxFold when TxFold.Show => CompareSetPanel.TxFold,
				CompareSetPanel.XGrid when XGrid.Show => CompareSetPanel.XGrid,
				_ => CompareSetPanel.Chart,
			};

		private IEnumerable<CompareSetPanel> VisiblePanels
		{
			get
			{
				yield return CompareSetPanel.Chart;
				if (TxFold.Show)
					yield return CompareSetPanel.TxFold;
				if (XGrid.Show)
					yield return CompareSetPanel.XGrid;
			}
		}

		private List<CompareSetPanel> SidePanels => VisiblePanels.Where(p => p != EffectiveMain).ToList();

		private bool IsMain(CompareSetPanel panel) => panel == EffectiveMain;

		private string PanelClass(CompareSetPanel panel) => IsMain(panel) ? "scmp-panel scmp-panel-main" : "scmp-panel scmp-panel-side";

		// Grid rows rather than moving markup, so swapping never re-mounts a chart or reloads TxFold's ticks.
		private string PanelStyle(CompareSetPanel panel) =>
			IsMain(panel) ? "grid-row: 1 / -1" : $"grid-row: {SidePanels.IndexOf(panel) + 1}";

		private string? BodyStyle => SidePanels.Count > 0 ? $"grid-template-rows: repeat({SidePanels.Count}, minmax(0, 1fr))" : null;

		private Task SetMainPanel(CompareSetPanel panel) => MainPanelChanged.InvokeAsync(panel);

		private List<CompareTxFoldSymbol> TxFoldSymbols() => Symbols.Select(s => new CompareTxFoldSymbol(s.Table, s.ColorVar, s.HistoryTable)).ToList();

		private IReadOnlyCollection<int> TxFoldSeconds => TxFold.Seconds ?? [.. SnapshotTimeframeOptions.DefaultSelectedSeconds];

		private Task SetTxFold(CompareSetTxFoldSettings settings) => TxFoldChanged.InvokeAsync(settings);

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
				(CompareSetMode.PercentChange, _) => "% change from the first bar in view",
				(_, CompareSetAnchor.WindowStart) when WindowStart is { } start => $"% change from the window start, {start:yyyy-MM-dd HH:mm}",
				(_, CompareSetAnchor.WindowStart) => "% change from each symbol's first bar",
				_ when WindowEnd is { } end => $"% change relative to the window end, {end:yyyy-MM-dd HH:mm}",
				_ => "% change relative to the latest bar",
			};

		private string ChartTooltip() =>
			(HasRatio
				? $"Every symbol's {ModeNote()}, with {CompareSetHelper.RatioLabel(Primary!.Table, RatioTable!)} in the lower pane"
				: $"Every symbol's {ModeNote()}")
			+ (TxFold.Show ? ". The highlighted zone and dashed lines mark where each TxFold window starts" : "");

		private Task OnModeSelected(ChangeEventArgs e) =>
			ModeChanged.InvokeAsync(Enum.TryParse<CompareSetMode>(e.Value?.ToString(), out var mode) ? mode : CompareSetMode.PercentChange);

		private Task OnAnchorSelected(ChangeEventArgs e) =>
			AnchorChanged.InvokeAsync(Enum.TryParse<CompareSetAnchor>(e.Value?.ToString(), out var anchor) ? anchor : CompareSetAnchor.WindowStart);

		private Task OnStyleSelected(ChangeEventArgs e) =>
			StyleChanged.InvokeAsync(Enum.TryParse<CompareChartSeriesStyle>(e.Value?.ToString(), out var style) ? style : CompareChartSeriesStyle.Lines);

		private Task OnRatioSelected(ChangeEventArgs e)
		{
			var table = e.Value?.ToString();
			return RatioTableChanged.InvokeAsync(string.IsNullOrEmpty(table) ? null : table);
		}
	}
}
