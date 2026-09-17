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

		private List<CTraderBarRow> _marketChartBars = new();
		private bool _marketChartLoading;
		private string? _marketChartError;
		private int _marketChartBarWidthSeconds = 60;
		private (string Table, DateTime From, DateTime To, int BarWidthSeconds)? _marketChartLoadedFor;

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
			if (_returnWindowMinutes <= 0)
			{
				_returnStartError = "window must be greater than 0";
				return;
			}

			_activeAnalysis = ActiveAnalysis.ReturnProbability;
			if (!ReturnService.TryStart(new PriceReturnRequest(_table, _from, _to, _returnWindowMinutes)))
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

		private IReadOnlyList<HistogramBar> ReturnChartBins =>
			ReturnService.Result is { } result
				? result.MinutesToReturnBins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList()
				: Array.Empty<HistogramBar>();

		private List<AnalizeBinRow> ReturnResultRows =>
			ReturnService.Result is { ReturnedCount: > 0 } result
				? result
					.MinutesToReturnBins.Select(b => new AnalizeBinRow(
						FormatMinutesBinRange(b),
						b.Count.ToString("N0"),
						(b.Count * 100.0 / result.ReturnedCount).ToString("N1") + "%"
					))
					.ToList()
				: new List<AnalizeBinRow>();

		private static string FormatMinutesBinRange(HistogramBin bin) =>
			$"{bin.Low.ToString("0.#", CultureInfo.InvariantCulture)}-{bin.High.ToString("0.#", CultureInfo.InvariantCulture)}m";

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private string TimeAboveBelowProgressPercentText =>
			TimeAboveBelowService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private string ReturnProgressPercentText => ReturnService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private async Task SelectPriceTab()
		{
			_activeMainTab = AnalizeMainTab.Price;
			if (_marketChartLoadedFor != (_table, _from, _to, _marketChartBarWidthSeconds))
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

			try
			{
				_marketChartBars = await Task.Run(() => FetchMarketChartBars(table, from, to, barWidthSeconds));
				_marketChartLoadedFor = (table, from, to, barWidthSeconds);
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

		private List<CTraderBarRow> FetchMarketChartBars(string table, DateTime from, DateTime to, int barWidthSeconds)
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
	}
}
