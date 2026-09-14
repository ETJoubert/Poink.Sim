using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Charts region of the live Trade page -- toolbar, lookback window, and the main chart/XGrid/volume-profile/time-profile panels for whichever symbol TradePage tells it to show. Owns bar aggregation and live-tick updates itself (subscribes to Session.TickReceived directly); pure window/VWAP/profile/XGrid-position math lives in ChartWindowHelper.</summary>
	public partial class TradeChartPanel : ComponentBase, IDisposable, IAsyncDisposable
	{
		/// <summary>Bar count a fresh chart load targets, bounding how much raw tick history is pulled per aggregation.</summary>
		private const int TargetHistoryBarCount = 300;

		/// <summary>Hard ceiling on the tick-history window regardless of bar size, so a coarse aggregation can't trigger a huge cTrader tick pull.</summary>
		private static readonly TimeSpan MaxHistoryLookback = TimeSpan.FromDays(14);

		/// <summary>Cap on the chart/XGrid lookback window, matching Simulation's bar-window cap.</summary>
		private const int MaxBarWindow = 300;

		[Inject]
		private ITickStreamAccess Session { get; set; } = null!;

		[Inject]
		private ICTraderHistory History { get; set; } = null!;

		[Inject]
		private IJSRuntime JS { get; set; } = null!;

		[Inject]
		private ITradeDraftService TradeDraft { get; set; } = null!;

		[Parameter]
		public long? SymbolId { get; set; }

		[Parameter]
		public string? SymbolName { get; set; }

		/// <summary>Real instrument pip size -- drives the price-histogram bucket width and XGrid's row height/pip-delta measurements alike.</summary>
		[Parameter]
		public double PipSize { get; set; } = 0.0001;

		[Parameter]
		public int BarSizeSeconds { get; set; } = 60;

		[Parameter]
		public EventCallback<int> BarSizeSecondsChanged { get; set; }

		[Parameter]
		public ChartPanelSettings? InitialSettings { get; set; }

		public ChartPanelSettings CurrentSettings =>
			new(
				_showMainChart,
				_showXGrid,
				_showVolumeProfile,
				_showTimeProfile,
				_showProfileChartLines,
				_showVwap,
				_barWindowSize,
				_barWindowOffset,
				_xgridPipCount,
				_xgridColCount
			);

		private bool _appliedInitialSettings;
		private long? _loadedSymbolId;
		private int _loadedBarSizeSeconds;

		private List<CTraderBarRow> _chartBars = new();
		private List<HlocAvdtBar> _chartHlocBars = new();
		private List<BarPriceHistogram> _chartHistograms = new();
		private bool _chartLoading;
		private string? _chartError;
		private SimulationBarAggregator? _barAggregator;

		private bool _showMainChart = true;
		private bool _showXGrid;
		private bool _showVolumeProfile;
		private bool _showTimeProfile;
		private bool _showProfileChartLines = true;
		private bool _showVwap = true;

		private int _barWindowSize = 50;
		private int _barWindowOffset;
		private int _xgridPipCount = 20;
		private int _xgridColCount = 20;
		private XGridResult? _xgridCache;
		private (
			int Pips,
			int Cols,
			int Offset,
			int Window,
			int Count,
			DateTime LastOpen,
			double LastClose,
			double PipSize
		)? _xgridKey;

		private string PopoutUrl => SymbolId is { } id
			? ChartPopoutUrlHelper.BuildUrl(id, SymbolName, BarSizeSeconds, CurrentSettings)
			: "";

		private string PopoutWindowName => $"poink-chart-popout-{SymbolId}";

		private int ClampedBarWindow => Math.Clamp(_barWindowSize, 1, MaxBarWindow);

		private int ClampedBarOffset => Math.Clamp(_barWindowOffset, 0, MaxBarWindow - 1);

		// Fits the chart once on the initial history load, then latches true so live ticks
		// update the bars in place without resetting the user's own pan/zoom.
		private bool _chartKeepView;

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private CTraderSymbolQuote? ChartQuote =>
			SymbolId is { } id ? Session.Quotes.FirstOrDefault(q => q.SymbolId == id) : null;

		protected override void OnInitialized()
		{
			Session.TickReceived += OnTickReceived;
			TradeDraft.Changed += OnDraftChanged;
		}

		private void OnDraftChanged() => InvokeAsync(StateHasChanged);

		protected override async Task OnParametersSetAsync()
		{
			if (!_appliedInitialSettings && InitialSettings is { } settings)
			{
				ApplySettings(settings);
				_appliedInitialSettings = true;
			}

			if (SymbolId == _loadedSymbolId && (SymbolId is null || BarSizeSeconds == _loadedBarSizeSeconds))
				return;

			_loadedSymbolId = SymbolId;
			_loadedBarSizeSeconds = BarSizeSeconds;

			if (SymbolId is { } id)
				await LoadChartHistoryAsync(id);
			else
				ResetChart();
		}

		private Task HandleBarSizeChanged(int seconds) => BarSizeSecondsChanged.InvokeAsync(seconds);

		private void ApplySettings(ChartPanelSettings settings)
		{
			_showMainChart = settings.ShowMainChart;
			_showXGrid = settings.ShowXGrid;
			_showVolumeProfile = settings.ShowVolumeProfile;
			_showTimeProfile = settings.ShowTimeProfile;
			_showProfileChartLines = settings.ShowProfileChartLines;
			_showVwap = settings.ShowVwap;
			_barWindowSize = settings.BarWindowSize;
			_barWindowOffset = settings.BarWindowOffset;
			_xgridPipCount = settings.XGridPipCount;
			_xgridColCount = settings.XGridColCount;
		}

		private ElementReference _chartRowRef;
		private ElementReference _chartDividerRef;
		private ElementReference _chartDividerRefVolume;
		private ElementReference _chartDividerRefTime;
		private IJSObjectReference? _dividerResizeInstance;
		private HashSet<string> _lastVisibleChartPanels = new();

		private HashSet<string> CurrentVisibleChartPanelKeys()
		{
			var keys = new HashSet<string>();
			if (_showMainChart) keys.Add("main");
			if (_showXGrid) keys.Add("xgrid");
			if (_showVolumeProfile) keys.Add("volume");
			if (_showTimeProfile) keys.Add("time");
			return keys;
		}

		// A divider's negative margin assumes a panel to its left, so a divider only shows
		// once an earlier panel (main -> XGrid -> volume profile) is visible.
		private bool ShowXGridDivider => _showMainChart && _showXGrid;

		private bool ShowVolumeProfileDivider => _showVolumeProfile && (_showMainChart || _showXGrid);

		private bool ShowTimeProfileDivider =>
			_showTimeProfile && (_showMainChart || _showXGrid || _showVolumeProfile);

		// Same drag-to-resize mechanism as SimulationPage's ChartDividerScript, trimmed to the
		// two-panel (main/side) case this chart row actually has -- no height divider here.
		private const string ChartDividerScript = """
			(function() {
			    var DIVIDER_CHROME_PX = 18;
			    function resetChartPanelsEqual(container) {
			        if (!container) return;
			        var panels = container.querySelectorAll(':scope > .trade-chart-main, :scope > .trade-chart-side');
			        if (panels.length === 0) return;
			        var dividerCount = container.querySelectorAll(':scope > .trade-chart-divider').length;
			        var style = getComputedStyle(container);
			        var containerWidth = container.getBoundingClientRect().width
			            - (parseFloat(style.paddingLeft) || 0)
			            - (parseFloat(style.paddingRight) || 0);
			        var reservedPercent = containerWidth > 0
			            ? (dividerCount * DIVIDER_CHROME_PX / containerWidth) * 100 : 0;
			        var percent = (100 - reservedPercent) / panels.length;
			        panels.forEach(function(p) { p.style.flex = '0 0 ' + percent + '%'; });
			    }
			    function bindChartDivider(divider) {
			        if (!divider || !divider.dataset || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minWidthPx = parseInt(divider.dataset.minWidth, 10) || 160;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var left = divider.previousElementSibling;
			            var right = divider.nextElementSibling;
			            if (!left || !right) return;
			            var container = divider.parentElement;
			            var containerStyle = getComputedStyle(container);
			            var containerWidth = container.getBoundingClientRect().width
			                - (parseFloat(containerStyle.paddingLeft) || 0)
			                - (parseFloat(containerStyle.paddingRight) || 0);
			            var minPercent = (minWidthPx / containerWidth) * 100;
			            var startX = e.clientX;
			            var startLeftPercent = (left.getBoundingClientRect().width / containerWidth) * 100;
			            var startRightPercent = (right.getBoundingClientRect().width / containerWidth) * 100;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('trade-chart-divider-active');

			            function onMove(ev) {
			                var deltaPercent = ((ev.clientX - startX) / containerWidth) * 100;
			                var newLeft = startLeftPercent + deltaPercent;
			                var newRight = startRightPercent - deltaPercent;
			                if (newLeft < minPercent) { newRight -= (minPercent - newLeft); newLeft = minPercent; }
			                if (newRight < minPercent) { newLeft -= (minPercent - newRight); newRight = minPercent; }
			                left.style.flex = '0 0 ' + newLeft + '%';
			                right.style.flex = '0 0 ' + newRight + '%';
			            }
			            function onUp(ev) {
			                divider.releasePointerCapture(e.pointerId);
			                divider.removeEventListener('pointermove', onMove);
			                divider.removeEventListener('pointerup', onUp);
			                divider.classList.remove('trade-chart-divider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    return { bindChartDivider: bindChartDivider, resetChartPanelsEqual: resetChartPanelsEqual };
			})()
			""";

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!_chartKeepView && _chartBars.Count > 0)
				_chartKeepView = true;

			if (firstRender)
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartDividerScript);

			if (_dividerResizeInstance is null || SymbolId is null || _chartLoading || _chartError is not null)
				return;

			var visiblePanels = CurrentVisibleChartPanelKeys();
			if (!visiblePanels.SetEquals(_lastVisibleChartPanels))
			{
				_lastVisibleChartPanels = visiblePanels;
				await _dividerResizeInstance.InvokeVoidAsync("resetChartPanelsEqual", _chartRowRef);
			}

			if (ShowXGridDivider)
				await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRef);
			if (ShowVolumeProfileDivider)
				await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRefVolume);
			if (ShowTimeProfileDivider)
				await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRefTime);
		}

		private async Task LoadChartHistoryAsync(long symbolId)
		{
			_chartError = null;
			_chartBars = new List<CTraderBarRow>();
			_chartHlocBars = new List<HlocAvdtBar>();
			_chartHistograms = new List<BarPriceHistogram>();
			_chartKeepView = false;
			_chartLoading = true;

			var barSize = TimeSpan.FromSeconds(BarSizeSeconds);
			_barAggregator = new SimulationBarAggregator(barSize, PipSize);

			try
			{
				var lookback = TimeSpan.FromTicks(
					Math.Min(barSize.Ticks * TargetHistoryBarCount, MaxHistoryLookback.Ticks)
				);
				var to = DateTime.UtcNow;
				var ticks = await History.GetHistoricalTicksAsync(symbolId, to - lookback, to);

				var bars = new List<CTraderBarRow>();
				var hlocBars = new List<HlocAvdtBar>();
				var histograms = new List<BarPriceHistogram>();
				foreach (var tick in ticks)
				{
					if (_barAggregator.Feed(tick) is { } completed)
					{
						bars.Add(ToBarRow(completed.Bar));
						hlocBars.Add(completed.Bar);
						histograms.Add(completed.Histogram);
					}
				}
				if (_barAggregator.CurrentPartialBar is { } partial)
				{
					bars.Add(ToBarRow(partial));
					hlocBars.Add(partial);
					histograms.Add(_barAggregator.CurrentPartialHistogram!);
				}

				_chartBars = bars;
				_chartHlocBars = hlocBars;
				_chartHistograms = histograms;

				if (_chartBars.Count == 0)
					_chartError = "No tick history returned for this symbol.";
			}
			catch (Exception ex)
			{
				_chartError = ex.Message;
			}
			finally
			{
				_chartLoading = false;
			}
		}

		private static CTraderBarRow ToBarRow(HlocAvdtBar bar) =>
			new(bar.OpenTime, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, bar.UpVolume, bar.DownVolume, bar.Distance);

		private void ResetChart()
		{
			_chartBars = new List<CTraderBarRow>();
			_chartHlocBars = new List<HlocAvdtBar>();
			_chartHistograms = new List<BarPriceHistogram>();
			_chartKeepView = false;
			_chartError = null;
			_barAggregator = null;
		}

		private void OnTickReceived(MarketTick tick)
		{
			// _chartLoading also covers a reload in flight, so a live tick can't reach the fresh
			// aggregator before LoadChartHistoryAsync backfills it in chronological order.
			if (tick.SymbolId != SymbolId || _barAggregator is null || _chartLoading)
				return;

			InvokeAsync(() =>
			{
				ApplyTickToChart(tick);
				StateHasChanged();
			});
		}

		/// <summary>Feeds a live tick into the chart's aggregator, finalizing the previous bar on a boundary crossing and updating or appending the forming bar.</summary>
		private void ApplyTickToChart(MarketTick tick)
		{
			if (_barAggregator is null)
				return;

			var completed = _barAggregator.Feed(new Tick(tick.TimestampUtc, tick.Bid, tick.Ask));
			var bars = new List<CTraderBarRow>(_chartBars);
			var hlocBars = new List<HlocAvdtBar>(_chartHlocBars);
			var histograms = new List<BarPriceHistogram>(_chartHistograms);

			if (completed is { } c)
			{
				if (bars.Count > 0)
				{
					bars[^1] = ToBarRow(c.Bar);
					hlocBars[^1] = c.Bar;
					histograms[^1] = c.Histogram;
				}

				if (_barAggregator.CurrentPartialBar is { } newPartial)
				{
					bars.Add(ToBarRow(newPartial));
					hlocBars.Add(newPartial);
					histograms.Add(_barAggregator.CurrentPartialHistogram!);
				}
			}
			else if (_barAggregator.CurrentPartialBar is { } partial)
			{
				if (bars.Count > 0)
				{
					bars[^1] = ToBarRow(partial);
					hlocBars[^1] = partial;
					histograms[^1] = _barAggregator.CurrentPartialHistogram!;
				}
				else
				{
					bars.Add(ToBarRow(partial));
					hlocBars.Add(partial);
					histograms.Add(_barAggregator.CurrentPartialHistogram!);
				}
			}

			_chartBars = bars;
			_chartHlocBars = hlocBars;
			_chartHistograms = histograms;
		}

		private List<WeightedPriceSample> MergedBarSamples() =>
			ChartWindowHelper.MergedBarSamples(_chartHlocBars, _chartHistograms, ClampedBarOffset, ClampedBarWindow);

		private List<WeightedTimeSample> MergedBarTimeSamples() =>
			ChartWindowHelper.MergedBarTimeSamples(_chartHlocBars, _chartHistograms, ClampedBarOffset, ClampedBarWindow);

		private XGridResult? ComputeXGrid()
		{
			var (bars, histograms) = ChartWindowHelper.WindowedBarData(_chartHlocBars, _chartHistograms, ClampedBarOffset);
			if (bars.Count == 0)
				return null;

			var window = Math.Min(ClampedBarWindow, bars.Count);
			var last = bars[^1];
			var key = (
				_xgridPipCount,
				_xgridColCount,
				ClampedBarOffset,
				window,
				bars.Count,
				last.OpenTime,
				last.Close,
				PipSize
			);
			if (_xgridKey == key && _xgridCache is not null)
				return _xgridCache;

			var start = bars[bars.Count - window].OpenTime;
			var end = last.OpenTime;
			_xgridKey = key;
			_xgridCache = XGridStats.Compute(bars, histograms, MergedBarSamples(), start, end, _xgridPipCount * PipSize, _xgridColCount);
			return _xgridCache;
		}

		private List<XGridPosition> XGridPositions() => ChartWindowHelper.XGridPositions(Session.Positions, SymbolId);

		private List<ChartHorizontalLine> ProfileLevelLines() => ChartWindowHelper.ProfileLevelLines(_showProfileChartLines, MergedBarSamples());

		private List<ChartHorizontalLine> ChartOverlayLines()
		{
			var lines = ProfileLevelLines();
			lines.AddRange(ChartWindowHelper.PositionLines(Session.Positions, SymbolId));
			lines.AddRange(ChartWindowHelper.DraftLines(TradeDraft.Draft, SymbolId));
			return lines;
		}

		private XGridExternalDraft? XGridDraft =>
			TradeDraft.Draft is { } d && d.SymbolId == SymbolId
				? new XGridExternalDraft(d.EntryPrice, d.IsLong, d.StopLossPrice, d.TakeProfitPrice)
				: null;

		private List<ChartLinePoint> VwapPoints() =>
			ChartWindowHelper.VwapPoints(_showVwap, _chartHlocBars, _chartHistograms, ClampedBarOffset, ClampedBarWindow);

		private DateTime? WindowStartUtc() =>
			ChartWindowHelper.WindowStartUtc(_chartHlocBars, _chartHistograms, ClampedBarOffset, ClampedBarWindow);

		private DateTime? WindowEndUtc() => ChartWindowHelper.WindowEndUtc(_chartHlocBars, _chartHistograms, ClampedBarOffset);

		public void Dispose()
		{
			Session.TickReceived -= OnTickReceived;
			TradeDraft.Changed -= OnDraftChanged;
		}

		public async ValueTask DisposeAsync()
		{
			if (_dividerResizeInstance is not null)
			{
				try
				{
					await _dividerResizeInstance.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
			}
		}
	}
}
