using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Charts region of the live Trade page -- toolbar, Indicators with the lookback window, and the main chart/XGrid/volume-profile/time-profile/TxFold panels for whichever symbol TradePage tells it to show, or the compare view across the watchlist. Bars come from the scoped ITradeBarFeed; pure window/XGrid-position math lives in ChartWindowHelper and TradeCompareHelper.</summary>
	public partial class TradeChartPanel : ComponentBase, IDisposable, IAsyncDisposable
	{
		/// <summary>Cap on the chart/XGrid lookback window, matching Simulation's bar-window cap.</summary>
		private const int MaxBarWindow = 300;

		/// <summary>Pip size for a compared symbol whose metadata hasn't loaded yet.</summary>
		private const double DefaultPipSize = 0.0001;

		[Inject]
		private ITickStreamAccess Session { get; set; } = null!;

		[Inject]
		private IWatchlistAccess Watchlist { get; set; } = null!;

		[Inject]
		private ITradeBarFeed BarFeed { get; set; } = null!;

		[Inject]
		private ISymbolMetadataCache MetadataCache { get; set; } = null!;

		[Inject]
		private ILogger<TradeChartPanel> Logger { get; set; } = null!;

		[Inject]
		private ITickRepository TickRepository { get; set; } = null!;

		[Inject]
		private ITodayTickReloadService TodayReload { get; set; } = null!;

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
				_barWindowSize,
				_barWindowOffset,
				_xgridPipCount,
				_xgridColCount,
				_indicators,
				_showTxFold,
				_txFoldSelectedSeconds.Order().ToList(),
				_showCompare,
				_compare
			);

		private bool _appliedInitialSettings;
		private long? _loadedSymbolId;
		private int _loadedBarSizeSeconds;

		private bool _chartLoading;
		private string? _chartError;
		private string? _chartWarning;

		private bool _showMainChart = true;
		private bool _showXGrid;
		private bool _showVolumeProfile;
		private bool _showTimeProfile;
		private bool _showTxFold;
		private IndicatorSettings _indicators = ChartPanelSettings.Default.Indicators;
		private HashSet<int> _txFoldSelectedSeconds = ChartPanelSettings.Default.TxFoldSeconds.ToHashSet();

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

		/// <summary>The charted symbol's series, once loaded at the current bar size.</summary>
		private TradeSymbolSeries? ChartSeries =>
			SymbolId is { } id && BarFeed.Get(id) is { } series && series.BarSizeSeconds == BarSizeSeconds ? series : null;

		private static readonly List<CTraderBarRow> NoBars = new();

		private List<CTraderBarRow> ChartBars => ChartSeries?.Bars ?? NoBars;

		private IReadOnlyList<HlocAvdtBar> ChartHlocBars => ChartSeries?.HlocBars ?? Array.Empty<HlocAvdtBar>();

		private IReadOnlyList<BarPriceHistogram> ChartHistograms => ChartSeries?.Histograms ?? Array.Empty<BarPriceHistogram>();

		private CTraderSymbolQuote? ChartQuote =>
			SymbolId is { } id ? Session.Quotes.FirstOrDefault(q => q.SymbolId == id) : null;

		protected override void OnInitialized()
		{
			_liveTickSource = new TradeLiveTickSource(TickRepository, BarFeed);
			BarFeed.SeriesChanged += OnSeriesChanged;
			BarFeed.Reset += OnFeedReset;
			Watchlist.Changed += OnWatchlistChanged;
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
			{
				await LoadChartHistoryAsync(id);
				_ = EnsureCompareSeriesAsync();
			}
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
			_barWindowSize = settings.BarWindowSize;
			_barWindowOffset = settings.BarWindowOffset;
			_xgridPipCount = settings.XGridPipCount;
			_xgridColCount = settings.XGridColCount;
			_indicators = settings.Indicators;
			_showTxFold = settings.ShowTxFold;
			_txFoldSelectedSeconds = settings.TxFoldSeconds.ToHashSet();
			_showCompare = settings.ShowCompare;
			_compare = settings.Compare;
		}

		private ElementReference _chartRowRef;
		private ElementReference _chartDividerRef;
		private ElementReference _chartDividerRefVolume;
		private ElementReference _chartDividerRefTime;
		private ElementReference _chartDividerRefTxFold;
		private IJSObjectReference? _dividerResizeInstance;
		private HashSet<string> _lastVisibleChartPanels = new();

		private HashSet<string> CurrentVisibleChartPanelKeys()
		{
			var keys = new HashSet<string>();
			if (_showMainChart) keys.Add("main");
			if (_showXGrid) keys.Add("xgrid");
			if (_showVolumeProfile) keys.Add("volume");
			if (_showTimeProfile) keys.Add("time");
			if (_showTxFold) keys.Add("txfold");
			return keys;
		}

		// A divider's negative margin assumes a panel to its left, so a divider only shows
		// once an earlier panel (main -> XGrid -> volume profile) is visible.
		private bool ShowXGridDivider => _showMainChart && _showXGrid;

		private bool ShowVolumeProfileDivider => _showVolumeProfile && (_showMainChart || _showXGrid);

		private bool ShowTimeProfileDivider =>
			_showTimeProfile && (_showMainChart || _showXGrid || _showVolumeProfile);

		private bool ShowTxFoldDivider =>
			_showTxFold && (_showMainChart || _showXGrid || _showVolumeProfile || _showTimeProfile);

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
			if (!_chartKeepView && ChartBars.Count > 0)
				_chartKeepView = true;

			if (firstRender)
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartDividerScript);

			if (_dividerResizeInstance is null || SymbolId is null || _chartLoading || _chartError is not null || CompareActive)
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
			if (ShowTxFoldDivider)
				await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRefTxFold);
		}

		private async Task LoadChartHistoryAsync(long symbolId)
		{
			_chartError = null;
			_chartWarning = null;
			_chartKeepView = false;
			_chartLoading = true;

			try
			{
				var series = await BarFeed.LoadAsync(symbolId, SymbolName ?? symbolId.ToString(), BarSizeSeconds, PipSize);
				_chartWarning = ChartHistoryWarning(series.History);
				if (series.Bars.Count == 0)
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

		private static string? ChartHistoryWarning(TickHistoryResult history)
		{
			var parts = new List<string>();
			if (history.MissingDays.Count > 0)
				parts.Add($"{history.HistoryTable} has no ticks for {string.Join(", ", history.MissingDays.Select(d => d.ToString("ddd yyyy-MM-dd")))} -- import them from the Data page.");
			if (history.ApiUnavailable)
				parts.Add("cTrader history is unavailable, so today's unstored ticks are missing.");
			return parts.Count == 0 ? null : string.Join(" ", parts);
		}

		private void ResetChart()
		{
			_chartKeepView = false;
			_chartError = null;
			_chartWarning = null;
		}

		private int _renderQueued;
		private int _syncRefreshToken;
		private bool _reloadingToday;
		private string? _reloadStatus;

		private string ReloadTodayTitle =>
			SymbolName is null
				? "Replace today's stored ticks with a fresh cTrader fetch"
				: $"Delete today's (UTC) stored ticks in {TodayReload.TableFor(SymbolName)} and replace them with a fresh cTrader fetch -- asks to confirm first";

		private async Task ReloadTodayAsync()
		{
			if (SymbolId is not { } id || SymbolName is null)
				return;

			var table = TodayReload.TableFor(SymbolName);
			if (!await JS.InvokeAsync<bool>("confirm", $"Replace today's stored {SymbolName} ticks in {table} with a fresh cTrader fetch? Today's recorded ticks will be deleted."))
				return;

			_reloadingToday = true;
			_reloadStatus = null;
			StateHasChanged();
			try
			{
				var result = await TodayReload.ReloadAsync(id, SymbolName);
				_reloadStatus = result.Succeeded ? $"Today reloaded: {result.Inserted:N0} tick(s), {result.Deleted:N0} replaced" : result.Error;
				if (result.Succeeded)
				{
					BarFeed.Invalidate(id);
					_syncRefreshToken++;
					await LoadChartHistoryAsync(id);
				}
			}
			finally
			{
				_reloadingToday = false;
			}
		}

		// The held bars belonged to the previous account, so the chart and compare series reload on the new one.
		private void OnFeedReset() =>
			InvokeAsync(async () =>
			{
				if (SymbolId is { } id)
				{
					await LoadChartHistoryAsync(id);
					await EnsureCompareSeriesAsync();
				}
				StateHasChanged();
			});

		private void OnSeriesChanged(long symbolId)
		{
			if (_chartLoading || (symbolId != SymbolId && !CompareActive))
				return;
			QueueRender();
		}

		// Ticks that land while a render is already queued are picked up by that render, so a burst across several symbols costs one render, not one per tick.
		private void QueueRender()
		{
			if (Interlocked.Exchange(ref _renderQueued, 1) == 1)
				return;

			InvokeAsync(() =>
			{
				Interlocked.Exchange(ref _renderQueued, 0);
				StateHasChanged();
			});
		}

		private List<WeightedPriceSample> MergedBarSamples() =>
			ChartWindowHelper.MergedBarSamples(ChartHlocBars, ChartHistograms, ClampedBarOffset, ClampedBarWindow);

		private List<WeightedTimeSample> MergedBarTimeSamples() =>
			ChartWindowHelper.MergedBarTimeSamples(ChartHlocBars, ChartHistograms, ClampedBarOffset, ClampedBarWindow);

		private XGridResult? ComputeXGrid()
		{
			var (bars, histograms) = ChartWindowHelper.WindowedBarData(ChartHlocBars, ChartHistograms, ClampedBarOffset);
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

		/// <summary>The indicators' market-chart lines plus this page's own position and draft levels.</summary>
		private IndicatorChartOverlays ChartOverlays()
		{
			var windowBars = ChartWindowHelper.WindowBars(ChartHlocBars, ChartHistograms, ClampedBarOffset, ClampedBarWindow);
			var overlays = IndicatorChartOverlayHelper.Build(_indicators, ChartHlocBars, windowBars, MergedBarSamples());
			overlays.HorizontalLines.AddRange(ChartWindowHelper.PositionLines(Session.Positions, SymbolId));
			overlays.HorizontalLines.AddRange(ChartWindowHelper.DraftLines(TradeDraft.Draft, SymbolId));
			return overlays;
		}

		private bool ShowVolumeProfileLevels => _indicators.PocVisible || _indicators.VahVisible || _indicators.ValVisible;

		private XGridExternalDraft? XGridDraft =>
			TradeDraft.Draft is { } d && d.SymbolId == SymbolId
				? new XGridExternalDraft(d.EntryPrice, d.IsLong, d.StopLossPrice, d.TakeProfitPrice)
				: null;

		private HlocAvdtBar? TxFoldAnchorBar()
		{
			var (bars, _) = ChartWindowHelper.WindowedBarData(ChartHlocBars, ChartHistograms, ClampedBarOffset);
			return TradeCompareHelper.TxFoldAnchorBar(bars, ClampedBarOffset);
		}

		private CTraderPositionInfo? ChartPosition => Session.Positions.LastOrDefault(p => p.SymbolId == SymbolId);

		private TradeDraft? ChartDraft => TradeDraft.Draft is { } d && d.SymbolId == SymbolId ? d : null;

		private DateTime? WindowStartUtc() =>
			ChartWindowHelper.WindowStartUtc(ChartHlocBars, ChartHistograms, ClampedBarOffset, ClampedBarWindow);

		private DateTime? WindowEndUtc() => ChartWindowHelper.WindowEndUtc(ChartHlocBars, ChartHistograms, ClampedBarOffset);

		public void Dispose()
		{
			BarFeed.SeriesChanged -= OnSeriesChanged;
			BarFeed.Reset -= OnFeedReset;
			Watchlist.Changed -= OnWatchlistChanged;
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
