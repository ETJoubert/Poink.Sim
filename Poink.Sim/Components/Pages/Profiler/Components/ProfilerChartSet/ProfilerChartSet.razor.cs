using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Models;
using Poink.Sim.Services.Charts;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// One Profiler symbol's full chart set: header, main chart, XGrid, volume/time profiles, subchart and TxFold,
	/// all driven by the page's global <see cref="ProfilerChartSetOptions"/> so every symbol is viewed the same way.
	/// </summary>
	public partial class ProfilerChartSet : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Inject]
		private IBarKindChartService BarKindChartService { get; set; } = default!;

		[Inject]
		private ILogger<ProfilerChartSet> Logger { get; set; } = default!;

		[Parameter, EditorRequired]
		public ProfilerSymbolData Data { get; set; } = default!;

		[Parameter, EditorRequired]
		public ProfilerChartSetOptions Options { get; set; } = default!;

		/// <summary>The primary set carries the page's drawings and its annotation tools; the others only offer the Lookback Window tool.</summary>
		[Parameter]
		public bool IsPrimary { get; set; }

		/// <summary>Stretches the set to fill the page column instead of taking a fixed share, for when it is the only set.</summary>
		[Parameter]
		public bool Fill { get; set; }

		[Parameter]
		public int XGridColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> XGridColCountChanged { get; set; }

		[Parameter]
		public int SubchartBarSizeSeconds { get; set; } = 10;

		[Parameter]
		public EventCallback<int> SubchartBarSizeSecondsChanged { get; set; }

		[Parameter]
		public BarAggregationKind SubchartBarKind { get; set; }

		[Parameter]
		public EventCallback<BarAggregationKind> SubchartBarKindChanged { get; set; }

		[Parameter]
		public IReadOnlyList<ChartHorizontalLine> DrawingHorizontalLines { get; set; } = Array.Empty<ChartHorizontalLine>();

		[Parameter]
		public IReadOnlyList<ChartVerticalLine> DrawingVerticalLines { get; set; } = Array.Empty<ChartVerticalLine>();

		[Parameter]
		public IReadOnlyList<ChartDiagonalLine> DrawingDiagonalLines { get; set; } = Array.Empty<ChartDiagonalLine>();

		[Parameter]
		public IReadOnlyList<ChartRangeFill> DrawingRangeFills { get; set; } = Array.Empty<ChartRangeFill>();

		[Parameter]
		public EventCallback<double> OnHorizontalLineDrawn { get; set; }

		[Parameter]
		public EventCallback<DateTime> OnVerticalLineDrawn { get; set; }

		[Parameter]
		public EventCallback<ChartDiagonalLine> OnDiagonalLineDrawn { get; set; }

		[Parameter]
		public EventCallback<ChartTimeRange> OnTRangeChartDrawn { get; set; }

		[Parameter]
		public EventCallback<DateTime> OnReferenceLineDrawn { get; set; }

		[Parameter]
		public EventCallback OnDrawFinished { get; set; }

		[Parameter]
		public EventCallback<ChartTimeRange> OnVolumeRangeDrawn { get; set; }

		[Parameter]
		public EventCallback OnVolumeRangeCleared { get; set; }

		[Parameter]
		public EventCallback OnNearStart { get; set; }

		[Parameter]
		public EventCallback<string> OnMakePrimary { get; set; }

		[Parameter]
		public EventCallback<string> OnRemove { get; set; }

		// Kept per set rather than shared, since one pip spans a different price distance on every symbol.
		private int _xgridPipCount = 20;

		private List<WeightedPriceSample> _samples = new();
		private List<WeightedTimeSample> _timeSamples = new();
		private List<ChartLinePoint> _vwapLine = new();
		private List<ChartHorizontalLine> _horizontalLines = new();
		private List<ChartVerticalLine> _verticalLines = new();
		private List<ChartDiagonalLine> _diagonalLines = new();
		private HashSet<TxFoldLevelKind> _txFoldLevels = new();
		private List<ChartRangeFill> _rangeFills = new();
		private (int Width, int Offset)? _windowInBars;

		private List<CTraderBarRow> _subchartBars = new();
		private List<ChartLinePoint> _subchartMedianLine = new();
		private bool _subchartLoading;
		private string? _subchartError;
		private (string Table, DateTime Start, DateTime End, int BarWidthSeconds, BarAggregationKind BarKind)? _subchartLoadedFor;

		private ElementReference _chartRowRef;
		private ElementReference _dividerRefXGrid;
		private ElementReference _dividerRefVolume;
		private ElementReference _dividerRefTime;
		private ElementReference _dividerRefSubchart;
		private ElementReference _dividerRefSnapshot;
		private ElementReference _resizeHandleRef;
		private IJSObjectReference? _dividerResizeInstance;
		private HashSet<string> _lastVisiblePanels = new();

		private DateTime? WindowStart => Options.WindowStart;

		private DateTime? WindowEnd => Options.WindowEnd;

		protected override void OnParametersSet()
		{
			_windowInBars = Options.LookbackEnabled && WindowStart is { } start && WindowEnd is { } end ? Data.WindowInBars(start, end) : null;
			_samples = ProfilerSymbolStatsHelper.MergedSamples(Data, WindowStart, WindowEnd);
			_timeSamples = Options.ShowTimeProfile ? ProfilerSymbolStatsHelper.MergedTimeSamples(Data, WindowStart, WindowEnd) : new();
			var indicators = Options.Indicators;
			var overlays = IndicatorChartOverlayHelper.Build(
				indicators,
				Data.Bars,
				ProfilerSymbolStatsHelper.WindowedBars(Data, WindowStart, WindowEnd),
				_samples
			);
			_vwapLine = overlays.VwapLine;
			_horizontalLines = DrawingHorizontalLines.Concat(overlays.HorizontalLines).ToList();
			_verticalLines = DrawingVerticalLines.Concat(overlays.VerticalLines).ToList();
			_diagonalLines = DrawingDiagonalLines.Concat(overlays.DiagonalLines).ToList();
			_txFoldLevels = IndicatorsHelper.ToTxFoldLevels(indicators);
			_rangeFills = DrawingRangeFills.Concat(LookbackWindowFill()).ToList();
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ProfilerChartDividerScript.Source);
				await _dividerResizeInstance.InvokeVoidAsync("bindVertical", _resizeHandleRef);
			}

			if (Data.HasBars && _dividerResizeInstance is not null)
			{
				var visiblePanels = CurrentVisiblePanelKeys();
				if (!visiblePanels.SetEquals(_lastVisiblePanels))
				{
					_lastVisiblePanels = visiblePanels;
					await _dividerResizeInstance.InvokeVoidAsync("resetEqual", _chartRowRef);
				}

				if (Options.ShowXGrid)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefXGrid);
				if (Options.ShowVolumeProfile)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefVolume);
				if (Options.ShowTimeProfile)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefTime);
				if (Options.ShowSubchart)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefSubchart);
				if (Options.ShowSnapshot)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefSnapshot);
			}

			await RefreshSubchartIfNeeded();
		}

		public async ValueTask DisposeAsync()
		{
			if (_dividerResizeInstance is null)
				return;
			try
			{
				await _dividerResizeInstance.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}

		private HashSet<string> CurrentVisiblePanelKeys()
		{
			var keys = new HashSet<string>();
			if (Options.ShowMainChart) keys.Add("main");
			if (Options.ShowXGrid) keys.Add("xgrid");
			if (Options.ShowVolumeProfile) keys.Add("volume");
			if (Options.ShowTimeProfile) keys.Add("time");
			if (Options.ShowSubchart) keys.Add("subchart");
			if (Options.ShowSnapshot) keys.Add("snapshot");
			return keys;
		}

		// Runs every render, so it only re-aggregates when the panel is open and the window or settings actually changed.
		private async Task RefreshSubchartIfNeeded()
		{
			if (!Options.ShowSubchart || WindowStart is not { } start || WindowEnd is not { } end || _subchartLoading)
				return;

			var key = (Data.Table, start, end, SubchartBarSizeSeconds, SubchartBarKind);
			if (_subchartLoadedFor == key)
				return;

			_subchartLoading = true;
			_subchartError = null;
			StateHasChanged();

			try
			{
				var (bars, medianLine) = await BarKindChartService.BuildBarsAsync(Data.Table, start, end, SubchartBarSizeSeconds, SubchartBarKind);
				_subchartBars = bars;
				_subchartMedianLine = medianLine;
				_subchartLoadedFor = key;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Failed to build the Profiler subchart for {Table}", Data.Table);
				_subchartError = ex.Message;
			}
			finally
			{
				_subchartLoading = false;
			}
			StateHasChanged();
		}

		private IEnumerable<ChartRangeFill> LookbackWindowFill()
		{
			if (WindowStart is { } start && WindowEnd is { } end)
				yield return new ChartRangeFill(start, end, "LOOKBACK WINDOW", "volume");
		}

		private XGridResult? ComputeXGrid() =>
			WindowStart is { } start && WindowEnd is { } end
				? Data.XGrid(start, end, _xgridPipCount * Data.EffectivePipSize, XGridColCount)
				: null;

		private string? WindowLabel() =>
			WindowStart is { } start && WindowEnd is { } end
				? $"{Data.BarCountIn(start, end):N0} bars, {ProfilerFormatHelper.FormatSpan(end - start)}"
				: null;

		private bool ShowVolumeProfileLevels =>
			Options.Indicators.PocVisible || Options.Indicators.VahVisible || Options.Indicators.ValVisible;

		private string PipSizeLabel() => Data.PipSize is { } pipSize ? $"pip {pipSize:0.#########}" : $"pip {ProfilerSymbolData.FallbackPipSize} (assumed)";

		private HlocAvdtBar? SnapshotAnchorBar() => WindowEnd is { } end ? Data.BarAtOrBefore(end) : null;
	}
}
