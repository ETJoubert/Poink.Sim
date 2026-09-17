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

		[Inject]
		private ITimeAboveBelowAnalysisService AnalysisService { get; set; } = default!;

		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		private bool _leftPanelCollapsed;
		private bool _rightPanelCollapsed;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;

		private double _windowMinutes = 60;
		private string? _startError;
		private AnalizeMainTab _activeMainTab;

		private List<CTraderBarRow> _marketChartBars = new();
		private bool _marketChartLoading;
		private string? _marketChartError;
		private int _marketChartBarWidthSeconds = 60;
		private (string Table, DateTime From, DateTime To, int BarWidthSeconds)? _marketChartLoadedFor;

		protected override void OnInitialized() => AnalysisService.OnChanged += HandleServiceChanged;

		public void Dispose() => AnalysisService.OnChanged -= HandleServiceChanged;

		private void HandleServiceChanged() => InvokeAsync(StateHasChanged);

		private void RunAnalysis()
		{
			_startError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_startError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_startError = "To must be after From";
				return;
			}
			if (_windowMinutes <= 0)
			{
				_startError = "window must be greater than 0";
				return;
			}

			if (!AnalysisService.TryStart(new TimeAboveBelowRequest(_table, _from, _to, _windowMinutes)))
				_startError = "analysis is already running";
		}

		private IReadOnlyList<HistogramBar> ChartBins =>
			AnalysisService.Result is { } result
				? result.Bins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList()
				: Array.Empty<HistogramBar>();

		private List<AnalizeBinRow> ResultRows =>
			AnalysisService.Result is { SampleCount: > 0 } result
				? result
					.Bins.Select(b => new AnalizeBinRow(
						FormatBinRange(b),
						b.Count.ToString("N0"),
						(b.Count * 100.0 / result.SampleCount).ToString("N1") + "%"
					))
					.ToList()
				: new List<AnalizeBinRow>();

		private static string FormatBinRange(HistogramBin bin) =>
			$"{(bin.Low * 100).ToString("0.#", CultureInfo.InvariantCulture)}-{(bin.High * 100).ToString("0.#", CultureInfo.InvariantCulture)}%";

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private string ProgressPercentText => AnalysisService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

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
