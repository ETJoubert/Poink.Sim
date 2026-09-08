using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Pages.Trade
{
	public partial class TradePage : ComponentBase, IDisposable
	{
		private const int MaxSymbolResults = 100;

		/// <summary>Bar count a fresh chart load targets, bounding how much raw tick history is pulled per aggregation.</summary>
		private const int TargetHistoryBarCount = 300;

		/// <summary>Hard ceiling on the tick-history window regardless of bar size, so a coarse aggregation can't trigger a huge cTrader tick pull.</summary>
		private static readonly TimeSpan MaxHistoryLookback = TimeSpan.FromDays(14);

		/// <summary>Bucket width for SimulationBarAggregator's price histogram; also the XGrid pip-size unit until a per-instrument pip size is looked up.</summary>
		private const double PriceBucketWidth = 0.01;

		/// <summary>Cap on the XGrid footprint's lookback window, matching Simulation's bar-window cap.</summary>
		private const int MaxXGridBarWindow = 300;

		[Inject]
		private ICTraderSessionService Session { get; set; } = null!;

		[Inject]
		private ICTraderHistory History { get; set; } = null!;

		[Inject]
		private NavigationManager Navigation { get; set; } = null!;

		private string _symbolFilter = string.Empty;

		private long? _chartSymbolId;
		private string? _chartSymbolName;
		private List<CTraderBarRow> _chartBars = new();
		private List<HlocAvdtBar> _chartHlocBars = new();
		private List<BarPriceHistogram> _chartHistograms = new();
		private bool _chartLoading;
		private string? _chartError;
		private int _barSizeSeconds = 60;
		private SimulationBarAggregator? _barAggregator;

		private bool _showXGrid;
		private int _xgridBarWindowSize = 50;
		private int _xgridBarWindowOffset;
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

		private int ClampedXGridWindow => Math.Clamp(_xgridBarWindowSize, 1, MaxXGridBarWindow);
		private int ClampedXGridOffset => Math.Clamp(_xgridBarWindowOffset, 0, MaxXGridBarWindow - 1);

		// Fits the chart once on the initial history load, then latches true so live ticks
		// update the bars in place without resetting the user's own pan/zoom.
		private bool _chartKeepView;

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

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

		protected override void OnAfterRender(bool firstRender)
		{
			if (!_chartKeepView && _chartBars.Count > 0)
				_chartKeepView = true;
		}

		private void LogIn()
		{
			if (Session.BuildAuthorizationUri() is not { } authUri)
				return;

			Navigation.NavigateTo(authUri.ToString(), forceLoad: true);
		}

		private Task LogOutAsync() => Session.LogoutAsync();

		private Task WatchAsync(long symbolId) => Session.WatchSymbolAsync(symbolId);

		private Task UnwatchAsync(long symbolId) => Session.UnwatchSymbolAsync(symbolId);

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

		private void OnSessionChanged() => InvokeAsync(StateHasChanged);

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

		// Slices the tracked bars/histograms to end Offset bars back from the live edge, matching
		// SimulationPage's WindowedBarData so the XGrid footprint reads the same lookback semantics.
		private (IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> Histograms) WindowedXGridData()
		{
			var offset = Math.Min(ClampedXGridOffset, Math.Max(0, _chartHlocBars.Count - 1));
			var count = _chartHlocBars.Count - offset;
			if (count <= 0)
				return (Array.Empty<HlocAvdtBar>(), Array.Empty<BarPriceHistogram>());
			return (_chartHlocBars.Take(count).ToList(), _chartHistograms.Take(count).ToList());
		}

		private List<WeightedPriceSample> MergedXGridSamples()
		{
			var (_, histograms) = WindowedXGridData();
			return BarPriceHistogram
				.Merge(histograms, ClampedXGridWindow)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
		}

		private XGridResult? ComputeXGrid()
		{
			var (bars, histograms) = WindowedXGridData();
			if (bars.Count == 0)
				return null;

			var window = Math.Min(ClampedXGridWindow, bars.Count);
			var last = bars[^1];
			var key = (
				_xgridPipCount,
				_xgridColCount,
				ClampedXGridOffset,
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
				MergedXGridSamples(),
				start,
				end,
				_xgridPipCount * PriceBucketWidth,
				_xgridColCount
			);
			return _xgridCache;
		}

		public void Dispose()
		{
			Session.Changed -= OnSessionChanged;
			Session.TickReceived -= OnTickReceived;
		}
	}
}
