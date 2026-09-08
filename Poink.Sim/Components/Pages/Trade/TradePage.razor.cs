using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Pages.Trade
{
	public partial class TradePage : ComponentBase, IDisposable, IAsyncDisposable
	{
		private const int MaxSymbolResults = 100;

		/// <summary>Bar count a fresh chart load targets, bounding how much raw tick history is pulled per aggregation.</summary>
		private const int TargetHistoryBarCount = 300;

		/// <summary>Hard ceiling on the tick-history window regardless of bar size, so a coarse aggregation can't trigger a huge cTrader tick pull.</summary>
		private static readonly TimeSpan MaxHistoryLookback = TimeSpan.FromDays(14);

		/// <summary>Bucket width for SimulationBarAggregator's price histogram; also the XGrid pip-size unit until a per-instrument pip size is looked up.</summary>
		private const double PriceBucketWidth = 0.01;

		/// <summary>Cap on the chart/XGrid lookback window, matching Simulation's bar-window cap.</summary>
		private const int MaxBarWindow = 300;

		[Inject]
		private ICTraderSessionService Session { get; set; } = null!;

		[Inject]
		private ICTraderHistory History { get; set; } = null!;

		[Inject]
		private NavigationManager Navigation { get; set; } = null!;

		[Inject]
		private IJSRuntime JS { get; set; } = null!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		private string _symbolFilter = string.Empty;
		private bool _sidepanelCollapsed;
		private bool _symbolPickerOpen;

		private long? _chartSymbolId;
		private string? _chartSymbolName;
		private List<CTraderBarRow> _chartBars = new();
		private List<HlocAvdtBar> _chartHlocBars = new();
		private List<BarPriceHistogram> _chartHistograms = new();
		private bool _chartLoading;
		private string? _chartError;
		private int _barSizeSeconds = 60;
		private SimulationBarAggregator? _barAggregator;

		private bool _showMainChart = true;
		private bool _showXGrid;
		private bool _showVolumeProfile;
		private bool _showTimeProfile;
		private bool _showTraderControls;
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
			double LastClose
		)? _xgridKey;

		private int ClampedBarWindow => Math.Clamp(_barWindowSize, 1, MaxBarWindow);
		private double BarWindowFillPercent => ClampedBarWindow * 100.0 / MaxBarWindow;

		private int ClampedBarOffset => Math.Clamp(_barWindowOffset, 0, MaxBarWindow - 1);
		private double BarWindowOffsetFillPercent => ClampedBarOffset * 100.0 / MaxBarWindow;

		// Fits the chart once on the initial history load, then latches true so live ticks
		// update the bars in place without resetting the user's own pan/zoom.
		private bool _chartKeepView;

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private bool _accountMenuOpen;

		private CTraderAccountInfo? SelectedAccount =>
			Session.Accounts.FirstOrDefault(a => a.AccountId == Session.SelectedAccountId);

		private void ToggleAccountMenu() => _accountMenuOpen = !_accountMenuOpen;

		private void CloseAccountMenu() => _accountMenuOpen = false;

		private void ToggleSidepanel() => _sidepanelCollapsed = !_sidepanelCollapsed;

		private void ToggleSymbolPicker() => _symbolPickerOpen = !_symbolPickerOpen;

		private CTraderSymbolQuote? ChartQuote =>
			_chartSymbolId is { } id ? Session.Watchlist.FirstOrDefault(q => q.SymbolId == id) : null;

		private IReadOnlyList<CTraderSymbolInfo> FilteredSymbols
		{
			get
			{
				var matches = string.IsNullOrWhiteSpace(_symbolFilter)
					? Session.Symbols
					: Session.Symbols.Where(s =>
						s.SymbolName.Contains(_symbolFilter, StringComparison.OrdinalIgnoreCase)
					);

				return matches.Take(MaxSymbolResults).ToList();
			}
		}

		private int TotalSymbolMatchCount =>
			string.IsNullOrWhiteSpace(_symbolFilter)
				? Session.Symbols.Count
				: Session.Symbols.Count(s => s.SymbolName.Contains(_symbolFilter, StringComparison.OrdinalIgnoreCase));

		private string StateLabel =>
			Session.State switch
			{
				CTraderConnectionState.Connecting => "CONNECTING",
				CTraderConnectionState.Connected => "CONNECTED",
				CTraderConnectionState.Failed => "FAILED",
				_ => "DISCONNECTED",
			};

		private string StateClass =>
			Session.State switch
			{
				CTraderConnectionState.Connecting => "trade-state-connecting",
				CTraderConnectionState.Connected => "trade-state-connected",
				CTraderConnectionState.Failed => "trade-state-failed",
				_ => "trade-state-disconnected",
			};

		protected override void OnInitialized()
		{
			Session.Changed += OnSessionChanged;
			Session.TickReceived += OnTickReceived;
		}

		private ElementReference _chartRowRef;
		private ElementReference _chartDividerRef;
		private ElementReference _chartDividerRefVolume;
		private ElementReference _chartDividerRefTime;
		private ElementReference _chartDividerRefTrader;
		private IJSObjectReference? _dividerResizeInstance;
		private HashSet<string> _lastVisibleChartPanels = new();

		private HashSet<string> CurrentVisibleChartPanelKeys()
		{
			var keys = new HashSet<string>();
			if (_showMainChart) keys.Add("main");
			if (_showXGrid) keys.Add("xgrid");
			if (_showVolumeProfile) keys.Add("volume");
			if (_showTimeProfile) keys.Add("time");
			if (_showTraderControls) keys.Add("trader");
			return keys;
		}

		// A divider's negative margin assumes a panel to its left, so a divider only shows
		// once an earlier panel (main -> XGrid -> volume profile -> time profile) is visible.
		private bool ShowXGridDivider => _showMainChart && _showXGrid;

		private bool ShowVolumeProfileDivider => _showVolumeProfile && (_showMainChart || _showXGrid);

		private bool ShowTimeProfileDivider =>
			_showTimeProfile && (_showMainChart || _showXGrid || _showVolumeProfile);

		private bool ShowTraderControlsDivider =>
			_showTraderControls && (_showMainChart || _showXGrid || _showVolumeProfile || _showTimeProfile);

		// Same drag-to-resize mechanism as SimulationPage's ChartDividerScript, trimmed to the
		// two-panel (main/side) case Trade's chart row actually has -- no height divider here.
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
			        if (!divider || divider.dataset.bound) return;
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
			{
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartDividerScript);
				await LoadPersistedSettingsAsync();
			}
			else
			{
				await PersistSettingsIfChangedAsync();
			}

			if (_chartSymbolId is null || _chartLoading || _chartError is not null || _dividerResizeInstance is null)
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
			if (ShowTraderControlsDivider)
				await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRefTrader);
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

		// Round-trips layout/chart settings and the watchlist through localStorage -- this component's
		// fields don't survive navigation, and CTraderSessionService's watchlist doesn't survive a restart.
		private const string SettingsStorageKey = "poink.trade.setup";

		private string? _lastPersistedSettingsJson;
		private bool _restorePending;
		private List<long> _pendingWatchSymbolIds = new();
		private long? _pendingChartSymbolId;
		private string? _pendingChartSymbolName;

		private TradePageSettings CurrentSettings =>
			new(
				_barSizeSeconds,
				_showMainChart,
				_showXGrid,
				_showVolumeProfile,
				_showTimeProfile,
				_showTraderControls,
				_showProfileChartLines,
				_showVwap,
				_barWindowSize,
				_barWindowOffset,
				_xgridPipCount,
				_xgridColCount,
				_chartSymbolId,
				_chartSymbolName,
				Session.Watchlist.Select(q => q.SymbolId).ToList(),
				_sidepanelCollapsed
			);

		private async Task LoadPersistedSettingsAsync()
		{
			var settings = await LocalStorage.GetItemAsync<TradePageSettings>(SettingsStorageKey);
			if (settings is null)
				return;

			_barSizeSeconds = settings.BarSizeSeconds;
			_showMainChart = settings.ShowMainChart;
			_showXGrid = settings.ShowXGrid;
			_showVolumeProfile = settings.ShowVolumeProfile;
			_showTimeProfile = settings.ShowTimeProfile;
			_showTraderControls = settings.ShowTraderControls;
			_showProfileChartLines = settings.ShowProfileChartLines;
			_showVwap = settings.ShowVwap;
			_barWindowSize = settings.BarWindowSize;
			_barWindowOffset = settings.BarWindowOffset;
			_xgridPipCount = settings.XGridPipCount;
			_xgridColCount = settings.XGridColCount;
			_sidepanelCollapsed = settings.SidepanelCollapsed;
			_pendingChartSymbolId = settings.ChartSymbolId;
			_pendingChartSymbolName = settings.ChartSymbolName;
			_pendingWatchSymbolIds = settings.WatchedSymbolIds;
			_restorePending = true;

			if (IsConnected)
				await ApplyPendingRestoreAsync();

			_lastPersistedSettingsJson = JsonSerializer.Serialize(CurrentSettings);
			StateHasChanged();
		}

		// Re-subscribes whatever this browser had watched and reopens its last chart -- called once
		// settings load if already connected, or on the next Connected transition otherwise.
		private async Task ApplyPendingRestoreAsync()
		{
			if (!_restorePending)
				return;
			_restorePending = false;

			foreach (var symbolId in _pendingWatchSymbolIds)
			{
				if (!Session.IsSymbolWatched(symbolId))
					await WatchAsync(symbolId);
			}

			if (_pendingChartSymbolId is { } id && _pendingChartSymbolName is { } name && _chartSymbolId is null)
				await OpenChartAsync(id, name);
		}

		private async Task PersistSettingsIfChangedAsync()
		{
			var json = JsonSerializer.Serialize(CurrentSettings);
			if (json == _lastPersistedSettingsJson)
				return;

			_lastPersistedSettingsJson = json;
			await LocalStorage.SetItemAsync(SettingsStorageKey, CurrentSettings);
		}

		private void LogIn()
		{
			if (Session.BuildAuthorizationUri() is not { } authUri)
				return;

			Navigation.NavigateTo(authUri.ToString(), forceLoad: true);
		}

		private async Task LogOutAsync()
		{
			_accountMenuOpen = false;
			await Session.LogoutAsync();
		}

		private Task WatchAsync(long symbolId) => Session.WatchSymbolAsync(symbolId);

		// Unwatching the symbol whose chart is open closes that chart too, since a chart
		// with no live quote behind it can no longer receive ticks.
		private async Task UnwatchAsync(long symbolId)
		{
			await Session.UnwatchSymbolAsync(symbolId);
			if (_chartSymbolId == symbolId)
				CloseChart();
		}

		private async Task OpenChartAsync(long symbolId, string symbolName)
		{
			_chartSymbolId = symbolId;
			_chartSymbolName = symbolName;
			await LoadChartHistoryAsync(symbolId);
		}

		private Task OnBarSizeChangedAsync() =>
			_chartSymbolId is { } symbolId ? LoadChartHistoryAsync(symbolId) : Task.CompletedTask;

		private async Task LoadChartHistoryAsync(long symbolId)
		{
			_chartError = null;
			_chartBars = new List<CTraderBarRow>();
			_chartHlocBars = new List<HlocAvdtBar>();
			_chartHistograms = new List<BarPriceHistogram>();
			_chartKeepView = false;
			_chartLoading = true;

			var barSize = TimeSpan.FromSeconds(_barSizeSeconds);
			_barAggregator = new SimulationBarAggregator(barSize, PriceBucketWidth);

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

		private void CloseChart()
		{
			_chartSymbolId = null;
			_chartSymbolName = null;
			_chartBars = new List<CTraderBarRow>();
			_chartHlocBars = new List<HlocAvdtBar>();
			_chartHistograms = new List<BarPriceHistogram>();
			_chartKeepView = false;
			_chartError = null;
			_barAggregator = null;
		}

		private static string Price(double? value) => value?.ToString("N5") ?? "—";

		private void OnSessionChanged() =>
			InvokeAsync(async () =>
			{
				if (_restorePending && IsConnected)
					await ApplyPendingRestoreAsync();
				StateHasChanged();
			});

		private void OnTickReceived(MarketTick tick)
		{
			// _chartLoading also covers a reload in flight, so a live tick can't reach the fresh
			// aggregator before LoadChartHistoryAsync backfills it in chronological order.
			if (tick.SymbolId != _chartSymbolId || _barAggregator is null || _chartLoading)
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

		// Every window-based view below (chart, XGrid, indicator overlays) reads from this
		// slice, so the Offset/Width slider moves them all together, matching SimulationPage.
		private (IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> Histograms) WindowedBarData()
		{
			var offset = Math.Min(ClampedBarOffset, Math.Max(0, _chartHlocBars.Count - 1));
			var count = _chartHlocBars.Count - offset;
			if (count <= 0)
				return (Array.Empty<HlocAvdtBar>(), Array.Empty<BarPriceHistogram>());
			return (_chartHlocBars.Take(count).ToList(), _chartHistograms.Take(count).ToList());
		}

		private List<WeightedPriceSample> MergedBarSamples()
		{
			var (_, histograms) = WindowedBarData();
			return BarPriceHistogram
				.Merge(histograms, ClampedBarWindow)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
		}

		private List<WeightedTimeSample> MergedBarTimeSamples()
		{
			var (bars, histograms) = WindowedBarData();
			return BarPriceHistogram
				.MergeTime(bars, histograms, ClampedBarWindow)
				.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
				.ToList();
		}

		private XGridResult? ComputeXGrid()
		{
			var (bars, histograms) = WindowedBarData();
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
				last.Close
			);
			if (_xgridKey == key && _xgridCache is not null)
				return _xgridCache;

			var start = bars[bars.Count - window].OpenTime;
			var end = last.OpenTime;
			_xgridKey = key;
			_xgridCache = XGridStats.Compute(
				bars,
				histograms,
				MergedBarSamples(),
				start,
				end,
				_xgridPipCount * PriceBucketWidth,
				_xgridColCount
			);
			return _xgridCache;
		}

		private List<ChartHorizontalLine> ProfileLevelLines()
		{
			if (!_showProfileChartLines)
				return new();
			if (VolumeProfileStats.Compute(MergedBarSamples()) is not { } levels)
				return new();
			return new()
			{
				new ChartHorizontalLine(levels.Poc, "POC", "profile"),
				new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
				new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
			};
		}

		private List<ChartLinePoint> VwapPoints()
		{
			if (!_showVwap)
				return new();

			var (bars, _) = WindowedBarData();
			var windowBars = bars.Count > ClampedBarWindow ? bars.Skip(bars.Count - ClampedBarWindow) : bars;

			var points = new List<ChartLinePoint>();
			double cumulativePriceVolume = 0,
				cumulativeVolume = 0;
			foreach (var bar in windowBars)
			{
				cumulativePriceVolume += bar.Average * bar.Volume;
				cumulativeVolume += bar.Volume;
				if (cumulativeVolume > 0)
					points.Add(new ChartLinePoint(bar.OpenTime, cumulativePriceVolume / cumulativeVolume));
			}
			return points;
		}

		private DateTime? WindowStartUtc()
		{
			var (bars, _) = WindowedBarData();
			if (bars.Count <= ClampedBarWindow)
				return null;
			return bars[bars.Count - ClampedBarWindow].OpenTime;
		}

		// Only meaningful once Offset pulls the window's far edge off of "now" -- at Offset 0
		// the end is just the newest bar, already the chart's own right edge.
		private DateTime? WindowEndUtc()
		{
			if (ClampedBarOffset <= 0)
				return null;
			var (bars, _) = WindowedBarData();
			return bars.Count > 0 ? bars[^1].OpenTime : null;
		}

		private static string QuoteRowTitle(CTraderSymbolQuote quote, bool selected)
		{
			var state = selected ? "Viewing this symbol's chart" : "Select to view this symbol's chart";
			var updated = quote.UpdatedAt?.ToString("HH:mm:ss") ?? "waiting...";
			return $"{state} -- last update: {updated}";
		}

		public void Dispose()
		{
			Session.Changed -= OnSessionChanged;
			Session.TickReceived -= OnTickReceived;
		}
	}
}
