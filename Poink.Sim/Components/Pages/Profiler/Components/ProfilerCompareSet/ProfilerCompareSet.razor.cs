using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Every selected Profiler symbol on one <see cref="CompareChart"/>, as % change by <see cref="Mode"/>,
	/// with an optional primary ÷ <see cref="RatioTable"/> line in a lower pane.
	/// </summary>
	public partial class ProfilerCompareSet : ComponentBase
	{
		/// <summary>Primary first, matching the per-symbol chart sets this replaces.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<ProfilerSymbolData> Sets { get; set; } = [];

		/// <summary>Table to theme colour var, shared with the symbols panel so it doubles as this chart's legend.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyDictionary<string, string> SymbolColors { get; set; } = new Dictionary<string, string>();

		[Parameter]
		public ProfilerCompareMode Mode { get; set; }

		[Parameter]
		public EventCallback<ProfilerCompareMode> ModeChanged { get; set; }

		/// <summary>Which Lookback Window edge <see cref="ProfilerCompareMode.WindowAnchored"/> measures from; ignored in the other mode.</summary>
		[Parameter]
		public ProfilerCompareAnchor WindowAnchor { get; set; }

		[Parameter]
		public EventCallback<ProfilerCompareAnchor> WindowAnchorChanged { get; set; }

		[Parameter]
		public CompareChartSeriesStyle Style { get; set; }

		[Parameter]
		public EventCallback<CompareChartSeriesStyle> StyleChanged { get; set; }

		/// <summary>The comparison symbol the primary is divided by for the ratio line; null or unselected hides it.</summary>
		[Parameter]
		public string? RatioTable { get; set; }

		[Parameter]
		public EventCallback<string?> RatioTableChanged { get; set; }

		[Parameter]
		public DateTime? WindowStart { get; set; }

		[Parameter]
		public DateTime? WindowEnd { get; set; }

		[Parameter]
		public EventCallback OnNearStart { get; set; }

		[Parameter]
		public ProfilerCompareTxFoldSettings TxFold { get; set; } = new();

		[Parameter]
		public EventCallback<ProfilerCompareTxFoldSettings> TxFoldChanged { get; set; }

		/// <summary>The panel picked for the main area; falls back to the % chart while the picked panel is hidden.</summary>
		[Parameter]
		public ProfilerComparePanel MainPanel { get; set; }

		[Parameter]
		public EventCallback<ProfilerComparePanel> MainPanelChanged { get; set; }

		[Parameter]
		public ProfilerCompareXGridSettings XGrid { get; set; } = new();

		[Parameter]
		public EventCallback<ProfilerCompareXGridSettings> XGridChanged { get; set; }

		/// <summary>Start of the loaded date range; TxFold windows reaching before it are drawn as partial.</summary>
		[Parameter]
		public DateTime RangeStart { get; set; }

		private List<CompareChartSeries> _series = new();
		private (
			List<HlocAvdtBar>[] Bars,
			IReadOnlyDictionary<string, string> Colors,
			ProfilerCompareMode Mode,
			DateTime? Anchor,
			string? RatioTable,
			CompareChartSeriesStyle Style
		)? _builtFor;

		private ProfilerSymbolData? Primary => Sets.Count > 0 ? Sets[0] : null;

		private IEnumerable<ProfilerSymbolData> Comparisons => Sets.Skip(1);

		// Without a window, End falls back to the primary's latest bar the way Start falls back to each first loaded bar.
		private DateTime? AnchorTime =>
			Mode != ProfilerCompareMode.WindowAnchored ? null
			: WindowAnchor == ProfilerCompareAnchor.WindowStart ? WindowStart
			: WindowEnd ?? (Primary is { HasBars: true } primary ? primary.Bars[^1].OpenTime : null);

		private bool CanLoadMore => Sets.Any(s => s.HasBars && !s.ReachedStart);

		private bool HasRatio => Comparisons.Any(s => s.Table == RatioTable);

		private CompareChartScaleMode ScaleMode =>
			Mode == ProfilerCompareMode.WindowAnchored ? CompareChartScaleMode.PercentValues : CompareChartScaleMode.Rebased;

		protected override void OnParametersSet()
		{
			var bars = Sets.Select(s => s.Bars).ToArray();
			if (_builtFor is { } built
				&& ReferenceEquals(built.Colors, SymbolColors)
				&& built.Mode == Mode
				&& built.Anchor == AnchorTime
				&& built.RatioTable == RatioTable
				&& built.Style == Style
				&& built.Bars.SequenceEqual(bars, ReferenceEqualityComparer.Instance))
				return;

			_builtFor = (bars, SymbolColors, Mode, AnchorTime, RatioTable, Style);
			_series = ProfilerCompareHelper.Build(Sets, SymbolColors, Mode, AnchorTime, RatioTable, Style);
		}

		private CompareXGridResult? _xgridResult;
		private (List<HlocAvdtBar>[] Bars, IReadOnlyDictionary<string, string> Colors, DateTime? Start, DateTime? End, DateTime? Anchor, double RowPercent, int ColCount)? _xgridBuiltFor;

		private bool AnchorApplies => Mode == ProfilerCompareMode.WindowAnchored || XGrid.Show;

		// Always anchored at the picked window edge, since a grid has no "first bar in view" to re-base on.
		private DateTime? XGridAnchor => WindowAnchor == ProfilerCompareAnchor.WindowEnd ? WindowEnd : WindowStart;

		private CompareXGridResult? XGridResult()
		{
			if (!XGrid.Show || WindowStart is not { } start || WindowEnd is not { } end || XGridAnchor is not { } anchor)
				return null;

			var bars = Sets.Select(s => s.Bars).ToArray();
			var key = (bars, SymbolColors, (DateTime?)start, (DateTime?)end, (DateTime?)anchor, XGrid.RowPercent, XGrid.ColCount);
			if (_xgridBuiltFor is { } built
				&& ReferenceEquals(built.Colors, SymbolColors)
				&& built.Start == start
				&& built.End == end
				&& built.Anchor == anchor
				&& built.RowPercent == XGrid.RowPercent
				&& built.ColCount == XGrid.ColCount
				&& built.Bars.SequenceEqual(bars, ReferenceEqualityComparer.Instance))
				return _xgridResult;

			_xgridBuiltFor = key;
			_xgridResult = ProfilerCompareHelper.BuildXGrid(Sets, SymbolColors, start, end, anchor, XGrid.RowPercent, XGrid.ColCount);
			return _xgridResult;
		}

		private string XGridNoDataMessage() =>
			WindowStart is null
				? $"Select a Lookback Window -- the grid covers it, with rows as % from each symbol's price at the window {(WindowAnchor == ProfilerCompareAnchor.WindowEnd ? "end" : "start")} (the Anchor setting)."
				: "No ticks inside the Lookback Window for any symbol.";

		private Task SetXGrid(ProfilerCompareXGridSettings settings) => XGridChanged.InvokeAsync(settings);

		private ProfilerComparePanel EffectiveMain =>
			MainPanel switch
			{
				ProfilerComparePanel.TxFold when TxFold.Show => ProfilerComparePanel.TxFold,
				ProfilerComparePanel.XGrid when XGrid.Show => ProfilerComparePanel.XGrid,
				_ => ProfilerComparePanel.Chart,
			};

		private IEnumerable<ProfilerComparePanel> VisiblePanels
		{
			get
			{
				yield return ProfilerComparePanel.Chart;
				if (TxFold.Show)
					yield return ProfilerComparePanel.TxFold;
				if (XGrid.Show)
					yield return ProfilerComparePanel.XGrid;
			}
		}

		private List<ProfilerComparePanel> SidePanels => VisiblePanels.Where(p => p != EffectiveMain).ToList();

		private bool IsMain(ProfilerComparePanel panel) => panel == EffectiveMain;

		private string PanelClass(ProfilerComparePanel panel) => IsMain(panel) ? "pcmp-panel pcmp-panel-main" : "pcmp-panel pcmp-panel-side";

		// Grid rows rather than moving markup, so swapping never re-mounts a chart or reloads TxFold's ticks.
		private string PanelStyle(ProfilerComparePanel panel) =>
			IsMain(panel) ? "grid-row: 1 / -1" : $"grid-row: {SidePanels.IndexOf(panel) + 1}";

		private string? BodyStyle => SidePanels.Count > 0 ? $"grid-template-rows: repeat({SidePanels.Count}, minmax(0, 1fr))" : null;

		private Task SetMainPanel(ProfilerComparePanel panel) => MainPanelChanged.InvokeAsync(panel);

		private string ColorVarFor(string table) => SymbolColorHelper.ColorVarFor(SymbolColors, table);

		private List<CompareTxFoldSymbol> TxFoldSymbols() => Sets.Select(s => new CompareTxFoldSymbol(s.Table, ColorVarFor(s.Table))).ToList();

		// ITickRepository's "to" is exclusive, so +1 tick keeps the anchor bar's own closing tick inside the window.
		private DateTime? TxFoldAnchor =>
			WindowEnd is { } end && Primary?.BarAtOrBefore(end) is { } bar ? bar.CloseTime.AddTicks(1) : null;

		private IReadOnlyCollection<int> TxFoldSeconds => TxFold.Seconds ?? [.. SnapshotTimeframeOptions.DefaultSelectedSeconds];

		private Task SetTxFold(ProfilerCompareTxFoldSettings settings) => TxFoldChanged.InvokeAsync(settings);

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
			(Mode, WindowAnchor) switch
			{
				(ProfilerCompareMode.PercentChange, _) => "% change from the first bar in view",
				(_, ProfilerCompareAnchor.WindowStart) when WindowStart is { } start => $"% change from the window start, {start:yyyy-MM-dd HH:mm}",
				(_, ProfilerCompareAnchor.WindowStart) => "% change from each symbol's first loaded bar -- turn on the Lookback Window to anchor it",
				_ when WindowEnd is { } end => $"% change relative to the window end, {end:yyyy-MM-dd HH:mm} -- the point TxFold anchors at",
				_ => "% change relative to the latest loaded bar -- turn on the Lookback Window to anchor it",
			};

		private string ChartTooltip() =>
			(HasRatio
				? $"Every symbol's {ModeNote()}, with {ProfilerCompareHelper.RatioLabel(Primary!.Table, RatioTable!)} in the lower pane -- pan left to backfill every symbol"
				: $"Every symbol's {ModeNote()} -- pan left to backfill every symbol")
			+ (TxFold.Show ? ". The highlighted zone and dashed lines mark where each TxFold window starts" : "");

		private Task OnModeSelected(ChangeEventArgs e) =>
			ModeChanged.InvokeAsync(Enum.TryParse<ProfilerCompareMode>(e.Value?.ToString(), out var mode) ? mode : ProfilerCompareMode.PercentChange);

		private Task OnAnchorSelected(ChangeEventArgs e) =>
			WindowAnchorChanged.InvokeAsync(Enum.TryParse<ProfilerCompareAnchor>(e.Value?.ToString(), out var anchor) ? anchor : ProfilerCompareAnchor.WindowStart);

		private Task OnStyleSelected(ChangeEventArgs e) =>
			StyleChanged.InvokeAsync(Enum.TryParse<CompareChartSeriesStyle>(e.Value?.ToString(), out var style) ? style : CompareChartSeriesStyle.Lines);

		private Task OnRatioSelected(ChangeEventArgs e)
		{
			var table = e.Value?.ToString();
			return RatioTableChanged.InvokeAsync(string.IsNullOrEmpty(table) ? null : table);
		}
	}
}
