using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Analysis;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Pages.Analize
{
	internal sealed record AnalizeBinRow(string Range, string Count, string PercentOfTotal);

	public partial class AnalizePage : ComponentBase, IDisposable
	{
		// Only used by SimulationBarAggregator's discarded histogram side-channel; the price chart never reads it.
		private const double PriceBucketWidth = 0.01;

		private enum AnalizeMainTab
		{
			Chart,
			Results,
			Price,
		}

		private enum ActiveAnalysis
		{
			TimeAboveBelow,
			ReturnProbability,
		}

		[Inject]
		private ITimeAboveBelowAnalysisService TimeAboveBelowService { get; set; } = default!;

		[Inject]
		private IPriceReturnAnalysisService ReturnService { get; set; } = default!;

		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		private bool _leftPanelCollapsed;
		private bool _rightPanelCollapsed;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;

		private AnalizeMainTab _activeMainTab;
		private ActiveAnalysis _activeAnalysis;

		private double _timeAboveBelowWindowMinutes = 60;
		private string? _timeAboveBelowStartError;
		private bool _timeAboveBelowCardCollapsed;

		private double _returnWindowMinutes = 30;
		private string? _returnStartError;
		private bool _returnCardCollapsed;
		private bool _returnUseAggregation;
		private BarAggregationKind _returnAggregationKind = BarAggregationKind.Ohlc;
		private int _returnAggregationBarWidthSeconds = 60;
		private int _returnWindowBars = 30;
		private string _returnSelectedSeries = "All";
		private static readonly string[] OhlcSeriesNames = { "Open", "High", "Low", "Close" };
		private static readonly string[] QuartileSeriesNames = { "Min", "Q1", "Median", "Q3", "Max" };

		private string[] ReturnSeriesNames =>
			_returnAggregationKind == BarAggregationKind.Quartile ? QuartileSeriesNames : OhlcSeriesNames;

		private string[] ReturnChartSeriesNames => new[] { "All" }.Concat(ReturnSeriesNames).ToArray();

		private static readonly Dictionary<string, string> ReturnSeriesColors = new()
		{
			["Open"] = "var(--color-info)",
			["High"] = "var(--color-success)",
			["Low"] = "var(--color-error)",
			["Close"] = "var(--color-primary)",
			["Min"] = "var(--color-error)",
			["Q1"] = "var(--color-info)",
			["Median"] = "var(--color-primary)",
			["Q3"] = "var(--color-secondary)",
			["Max"] = "var(--color-success)",
		};

		private List<CTraderBarRow> _marketChartBars = new();
		private List<ChartLinePoint> _marketChartMedianLine = new();
		private bool _marketChartLoading;
		private string? _marketChartError;
		private int _marketChartBarWidthSeconds = 60;
		private BarAggregationKind _marketChartBarKind = BarAggregationKind.Ohlc;
		private (string Table, DateTime From, DateTime To, int BarWidthSeconds, BarAggregationKind BarKind)? _marketChartLoadedFor;

		protected override void OnInitialized()
		{
			TimeAboveBelowService.OnChanged += HandleServiceChanged;
			ReturnService.OnChanged += HandleServiceChanged;
		}

		public void Dispose()
		{
			TimeAboveBelowService.OnChanged -= HandleServiceChanged;
			ReturnService.OnChanged -= HandleServiceChanged;
		}

		private void HandleServiceChanged() => InvokeAsync(StateHasChanged);

		private void RunTimeAboveBelow()
		{
			_timeAboveBelowStartError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_timeAboveBelowStartError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_timeAboveBelowStartError = "To must be after From";
				return;
			}
			if (_timeAboveBelowWindowMinutes <= 0)
			{
				_timeAboveBelowStartError = "window must be greater than 0";
				return;
			}

			_activeAnalysis = ActiveAnalysis.TimeAboveBelow;
			if (!TimeAboveBelowService.TryStart(new TimeAboveBelowRequest(_table, _from, _to, _timeAboveBelowWindowMinutes)))
				_timeAboveBelowStartError = "analysis is already running";
		}

		private void RunReturnProbability()
		{
			_returnStartError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_returnStartError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_returnStartError = "To must be after From";
				return;
			}

			PriceReturnRequest request;
			if (_returnUseAggregation)
			{
				if (_returnWindowBars <= 0)
				{
					_returnStartError = "window (bars) must be greater than 0";
					return;
				}
				request = new PriceReturnRequest(_table, _from, _to, 0, _returnAggregationBarWidthSeconds, _returnWindowBars, _returnAggregationKind);
			}
			else
			{
				if (_returnWindowMinutes <= 0)
				{
					_returnStartError = "window must be greater than 0";
					return;
				}
				request = new PriceReturnRequest(_table, _from, _to, _returnWindowMinutes);
			}

			_activeAnalysis = ActiveAnalysis.ReturnProbability;
			if (!ReturnService.TryStart(request))
				_returnStartError = "analysis is already running";
		}

		private IReadOnlyList<HistogramBar> TimeAboveBelowChartBins =>
			TimeAboveBelowService.Result is { } result
				? result.Bins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList()
				: Array.Empty<HistogramBar>();

		private List<AnalizeBinRow> TimeAboveBelowResultRows =>
			TimeAboveBelowService.Result is { SampleCount: > 0 } result
				? result
					.Bins.Select(b => new AnalizeBinRow(
						FormatPercentBinRange(b),
						b.Count.ToString("N0"),
						(b.Count * 100.0 / result.SampleCount).ToString("N1") + "%"
					))
					.ToList()
				: new List<AnalizeBinRow>();

		private static string FormatPercentBinRange(HistogramBin bin) =>
			$"{(bin.Low * 100).ToString("0.#", CultureInfo.InvariantCulture)}-{(bin.High * 100).ToString("0.#", CultureInfo.InvariantCulture)}%";

		// Aggregation mode carries a per-series breakdown (Open/High/Low/Close); tick mode has none,
		// so the top-level (Close-equivalent) bins are used directly.
		private (IReadOnlyList<HistogramBin> Bins, long ReturnedCount, string Unit) SelectedReturnSeries
		{
			get
			{
				if (ReturnService.Result is not { } result)
					return (Array.Empty<HistogramBin>(), 0, "min");
				if (result.SeriesBreakdown is { } breakdown)
				{
					var series = breakdown.FirstOrDefault(s => s.Series == _returnSelectedSeries) ?? breakdown[0];
					return (series.ToReturnBins, series.ReturnedCount, result.ToReturnUnit);
				}
				return (result.ToReturnBins, result.ReturnedCount, result.ToReturnUnit);
			}
		}

		private IReadOnlyList<HistogramBar> ReturnChartBins =>
			SelectedReturnSeries.Bins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList();

		private IReadOnlyList<HistogramSeries> ReturnAllSeriesChart =>
			ReturnService.Result?.SeriesBreakdown is { } breakdown
				? breakdown
					.Select(s => new HistogramSeries(
						s.Series,
						ReturnSeriesColors.GetValueOrDefault(s.Series, "var(--color-secondary)"),
						s.ToReturnBins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList()
					))
					.ToList()
				: Array.Empty<HistogramSeries>();

		private List<AnalizeBinRow> ReturnResultRows
		{
			get
			{
				var (bins, returnedCount, unit) = SelectedReturnSeries;
				return returnedCount == 0
					? new List<AnalizeBinRow>()
					: bins
						.Select(b => new AnalizeBinRow(
							FormatToReturnBinRange(b, unit),
							b.Count.ToString("N0"),
							(b.Count * 100.0 / returnedCount).ToString("N1") + "%"
						))
						.ToList();
			}
		}

		private static string FormatToReturnBinRange(HistogramBin bin, string unit) =>
			unit == "bars"
				? $"bar {(bin.Low + 1).ToString("0.#", CultureInfo.InvariantCulture)}"
				: FormatMinutesBinRange(bin);

		// Bars are stored 0-indexed for bucketing; +1 shows the human-friendly "1st bar after start" count.
		private static string FormatToReturnValue(double value, string unit) =>
			unit == "bars" ? $"{(value + 1).ToString("N1")} bars" : $"{value.ToString("N1")} min";

		private static string FormatMinutesBinRange(HistogramBin bin) =>
			$"{bin.Low.ToString("0.#", CultureInfo.InvariantCulture)}-{bin.High.ToString("0.#", CultureInfo.InvariantCulture)}m";

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private string TimeAboveBelowProgressPercentText =>
			TimeAboveBelowService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private string ReturnProgressPercentText => ReturnService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private string ReturnWindowBarsMinutesText => (_returnAggregationBarWidthSeconds * _returnWindowBars / 60.0).ToString("N1");

		private string ReturnEffectiveWindowText =>
			_returnUseAggregation ? $"{_returnWindowBars} bars ({ReturnWindowBarsMinutesText} min)" : $"{_returnWindowMinutes} min";

		private async Task SelectPriceTab()
		{
			_activeMainTab = AnalizeMainTab.Price;
			if (_marketChartLoadedFor != (_table, _from, _to, _marketChartBarWidthSeconds, _marketChartBarKind))
				await LoadMarketChartAsync();
		}

		private async Task LoadMarketChartAsync()
		{
			_marketChartError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_marketChartError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_marketChartError = "To must be after From";
				return;
			}

			_marketChartLoading = true;
			var table = _table;
			var from = _from;
			var to = _to;
			var barWidthSeconds = _marketChartBarWidthSeconds;
			var barKind = _marketChartBarKind;

			try
			{
				var (bars, medianLine) = await Task.Run(() => FetchMarketChartBars(table, from, to, barWidthSeconds, barKind));
				_marketChartBars = bars;
				_marketChartMedianLine = medianLine;
				_marketChartLoadedFor = (table, from, to, barWidthSeconds, barKind);
			}
			catch (Exception ex)
			{
				_marketChartError = ex.Message;
			}
			finally
			{
				_marketChartLoading = false;
			}
		}

		private (List<CTraderBarRow> Bars, List<ChartLinePoint> MedianLine) FetchMarketChartBars(
			string table,
			DateTime from,
			DateTime to,
			int barWidthSeconds,
			BarAggregationKind barKind
		) =>
			barKind == BarAggregationKind.Quartile
				? FetchQuartileMarketChartBars(table, from, to, barWidthSeconds)
				: (FetchOhlcMarketChartBars(table, from, to, barWidthSeconds), new List<ChartLinePoint>());

		private List<CTraderBarRow> FetchOhlcMarketChartBars(string table, DateTime from, DateTime to, int barWidthSeconds)
		{
			var aggregator = new SimulationBarAggregator(TimeSpan.FromSeconds(barWidthSeconds), PriceBucketWidth);
			var bars = new List<HlocAvdtBar>();
			foreach (var tick in TickRepository.ReadTicks(table, from, to))
				if (aggregator.Feed(tick) is { } completed)
					bars.Add(completed.Bar);
			if (aggregator.CurrentPartialBar is { } partial)
				bars.Add(partial);

			return bars
				.Select(b => new CTraderBarRow(b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance))
				.ToList();
		}

		// Reuses the candlestick chart as a box plot (Open/Close = Q1/Q3, High/Low = Min/Max, see the price
		// toolbar caption); Median has no room left in that OHLC shape, so it rides along as its own overlay line.
		private (List<CTraderBarRow> Bars, List<ChartLinePoint> MedianLine) FetchQuartileMarketChartBars(
			string table,
			DateTime from,
			DateTime to,
			int barWidthSeconds
		)
		{
			var aggregator = new QuartileBarAggregator(TimeSpan.FromSeconds(barWidthSeconds));
			var bars = new List<QuartileBar>();
			foreach (var tick in TickRepository.ReadTicks(table, from, to))
				if (aggregator.Feed(tick) is { } completed)
					bars.Add(completed);
			if (aggregator.CurrentPartialBar is { } partial)
				bars.Add(partial);

			var rows = bars.Select(b => new CTraderBarRow(b.OpenTime, b.Q1, b.Max, b.Min, b.Q3, b.Count)).ToList();
			var medianLine = bars.Select(b => new ChartLinePoint(b.OpenTime, b.Median)).ToList();
			return (rows, medianLine);
		}
	}
}
