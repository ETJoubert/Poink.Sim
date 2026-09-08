using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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

		/// <summary>Unused for chart display; SimulationBarAggregator requires a bucket width for its price histogram.</summary>
		private const double PriceBucketWidth = 0.01;

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
		private bool _chartLoading;
		private string? _chartError;
		private int _barSizeSeconds = 60;
		private SimulationBarAggregator? _barAggregator;

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
				foreach (var tick in ticks)
				{
					if (_barAggregator.Feed(tick) is { } completed)
						bars.Add(ToBarRow(completed.Bar));
				}
				if (_barAggregator.CurrentPartialBar is { } partial)
					bars.Add(ToBarRow(partial));

				_chartBars = bars;

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

			if (completed is { } c)
			{
				if (bars.Count > 0)
					bars[^1] = ToBarRow(c.Bar);

				if (_barAggregator.CurrentPartialBar is { } newPartial)
					bars.Add(ToBarRow(newPartial));
			}
			else if (_barAggregator.CurrentPartialBar is { } partial)
			{
				if (bars.Count > 0)
					bars[^1] = ToBarRow(partial);
				else
					bars.Add(ToBarRow(partial));
			}

			_chartBars = bars;
		}

		public void Dispose()
		{
			Session.Changed -= OnSessionChanged;
			Session.TickReceived -= OnTickReceived;
		}
	}
}
