using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.MarketData.History;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// The compare view that replaces the chart row while on: the charted symbol and every other watched symbol's live bars
	/// on one shared <see cref="CompareSet"/>, coloured like the watchlist so its cards are the legend. Every panel follows
	/// each tick: the % chart and XGrid from the bars, and at Offset 0 the TxFold from <see cref="TradeLiveTickSource"/>.
	/// </summary>
	public partial class TradeChartPanel
	{
		private bool _showCompare;
		private TradeCompareSettings _compare = new();
		private List<CompareSetSymbol> _compareSymbols = new();
		private CompareXGridResult? _compareXGridResult;
		private (DateTime Start, DateTime End, DateTime Anchor, double RowPercent, int ColCount, int SymbolCount)? _compareXGridKey;
		private List<IReadOnlyList<HlocAvdtBar>> _compareXGridBars = new();
		private bool _compareLoading;
		private readonly HashSet<long> _compareLoadFailed = new();

		private CompareSetMode CompareMode
		{
			get => _compare.Mode;
			set => _compare = _compare with { Mode = value };
		}

		private CompareSetAnchor CompareAnchor
		{
			get => _compare.Anchor;
			set => _compare = _compare with { Anchor = value };
		}

		private CompareChartSeriesStyle CompareStyle
		{
			get => _compare.Style;
			set => _compare = _compare with { Style = value };
		}

		private string? CompareRatioTable
		{
			get => _compare.RatioTable;
			set => _compare = _compare with { RatioTable = value };
		}

		private CompareSetTxFoldSettings CompareTxFold
		{
			get => _compare.TxFold ?? new();
			set => _compare = _compare with { TxFold = value };
		}

		private CompareSetXGridSettings CompareXGridSettings
		{
			get => _compare.XGrid ?? new();
			set => _compare = _compare with { XGrid = value };
		}

		private CompareSetPanel CompareMainPanel
		{
			get => _compare.MainPanel;
			set => _compare = _compare with { MainPanel = value };
		}

		private bool CompareActive => _showCompare && SymbolId is not null;

		private IReadOnlyDictionary<string, string> WatchlistColors => SymbolColorHelper.Assign(Watchlist.Watchlist.Select(q => q.SymbolName));

		/// <summary>The charted symbol first, then every other watched symbol whose bars have loaded at the current bar size.</summary>
		private IReadOnlyList<TradeSymbolSeries> CompareSeries()
		{
			var series = new List<TradeSymbolSeries>();
			if (ChartSeries is { } chart)
				series.Add(chart);
			foreach (var quote in Watchlist.Watchlist.Where(q => q.SymbolId != SymbolId))
				if (BarFeed.Get(quote.SymbolId) is { } watched && watched.BarSizeSeconds == BarSizeSeconds)
					series.Add(watched);
			return series;
		}

		private IReadOnlyList<CompareSetSymbol> CompareSymbols()
		{
			var colors = WatchlistColors;
			var symbols = CompareSeries()
				.Select(s => new CompareSetSymbol(
					s.History.TodayTable,
					SymbolColorHelper.ColorVarFor(colors, s.SymbolName),
					s.Bars,
					s.History.HistoryTable == s.History.TodayTable ? null : s.History.HistoryTable
				))
				.ToList();

			if (!symbols.SequenceEqual(_compareSymbols))
				_compareSymbols = symbols;
			return _compareSymbols;
		}

		private int PendingCompareSymbolCount =>
			Watchlist.Watchlist.Count(q =>
				q.SymbolId != SymbolId
				&& !_compareLoadFailed.Contains(q.SymbolId)
				&& (BarFeed.Get(q.SymbolId) is not { } s || s.BarSizeSeconds != BarSizeSeconds)
			);

		/// <summary>The charted symbol's Lookback Window edges as bar open times, shared by every compared symbol.</summary>
		private (DateTime? Start, DateTime? End) LookbackWindowTimes()
		{
			var bars = ChartWindowHelper.WindowBars(ChartHlocBars, ChartHistograms, ClampedBarOffset, ClampedBarWindow);
			return bars.Count == 0 ? (null, null) : (bars[0].OpenTime, bars[^1].OpenTime);
		}

		private CompareXGridResult? CompareXGrid()
		{
			var (start, end) = LookbackWindowTimes();
			var anchor = CompareAnchor == CompareSetAnchor.WindowEnd ? end : start;
			if (!CompareXGridSettings.Show || start is not { } windowStart || end is not { } windowEnd || anchor is not { } anchorTime)
				return null;

			var colors = WatchlistColors;
			var sources = CompareSeries()
				.Select(s => new TradeCompareXGridSource(s.History.TodayTable, SymbolColorHelper.ColorVarFor(colors, s.SymbolName), s.HlocBars, s.Histograms))
				.ToList();
			var settings = CompareXGridSettings;
			var key = (windowStart, windowEnd, anchorTime, settings.RowPercent, settings.ColCount, sources.Count);
			var bars = sources.Select(s => s.Bars).ToList();
			if (_compareXGridKey == key && _compareXGridBars.SequenceEqual(bars, ReferenceEqualityComparer.Instance))
				return _compareXGridResult;

			_compareXGridKey = key;
			_compareXGridBars = bars;
			_compareXGridResult = TradeCompareHelper.ComputeXGrid(sources, windowStart, windowEnd, anchorTime, settings.RowPercent, settings.ColCount);
			return _compareXGridResult;
		}

		private TradeLiveTickSource? _liveTickSource;

		/// <summary>At Offset 0 the TxFold windows end just past the newest tick of any compared symbol; otherwise at the window's last bar.</summary>
		private DateTime? CompareTxFoldAnchor() =>
			ClampedBarOffset == 0
				? CompareSeries().Select(s => s.LastTickUtc).Max() is { } latest ? latest.AddTicks(1) : null
				: TxFoldAnchorBar() is { } bar ? bar.CloseTime.AddTicks(1) : null;

		private ICompareTxFoldTickSource? CompareTxFoldTickSource => ClampedBarOffset == 0 ? _liveTickSource : null;

		private static DateTime HistoryBeforeUtc => TickHistoryPlanner.DayStartUtc(DateTime.UtcNow);

		private async Task ToggleCompareAsync()
		{
			_showCompare = !_showCompare;
			if (_showCompare)
				await EnsureCompareSeriesAsync();
		}

		/// <summary>
		/// Loads every watched symbol's bars at the current bar size, one at a time so their history reads don't pile up,
		/// and keeps going until symbols watched mid-load are covered too.
		/// </summary>
		private async Task EnsureCompareSeriesAsync()
		{
			if (!CompareActive || _compareLoading)
				return;

			_compareLoading = true;
			_compareLoadFailed.Clear();
			var attempted = new HashSet<(long SymbolId, int BarSizeSeconds)>();
			try
			{
				while (Watchlist.Watchlist.FirstOrDefault(q => q.SymbolId != SymbolId && attempted.Add((q.SymbolId, BarSizeSeconds))) is { } quote)
				{
					try
					{
						await BarFeed.LoadAsync(quote.SymbolId, quote.SymbolName, BarSizeSeconds, PipSizeFor(quote.SymbolId));
					}
					catch (Exception ex)
					{
						_compareLoadFailed.Add(quote.SymbolId);
						Logger.LogError(ex, "Failed to load compare bars for {Symbol}.", quote.SymbolName);
					}
					await InvokeAsync(StateHasChanged);
				}
			}
			finally
			{
				_compareLoading = false;
			}
		}

		private double PipSizeFor(long symbolId) =>
			MetadataCache.Get(symbolId) is { } metadata ? PositionPnlHelper.PipSize(metadata) : DefaultPipSize;

		private void OnWatchlistChanged() =>
			InvokeAsync(async () =>
			{
				var keep = Watchlist.Watchlist.Select(q => q.SymbolId).ToHashSet();
				if (SymbolId is { } chartId)
					keep.Add(chartId);
				BarFeed.RetainOnly(keep);

				if (CompareActive)
					await EnsureCompareSeriesAsync();
				StateHasChanged();
			});

	}
}
