using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Poink.Sim.Analysis;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.Charts;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Analize
{
	internal sealed record AnalizeBinRow(string Range, string Count, string PercentOfTotal);

	internal sealed record BarrierResultRow(
		string StopPips,
		string TargetPips,
		string Trades,
		string Won,
		string Stopped,
		string TimedOut,
		string WinRate,
		string ScrambledWinRate,
		string Edge,
		string AvgPips,
		string ScrambledAvgPips,
		string EdgePips,
		string Ci95,
		string AvgMinutes
	);

	// Top-level (not nested in AnalizePage) and public because AnalysisDetailCard's [Parameter]
	// can't expose a less-accessible type -- a Razor component's generated class is always public.
	public enum ActiveAnalysis
	{
		TimeAboveBelow,
		ReturnProbability,
		BarrierMap,
	}

	public sealed record AnalysisSeriesSummaryView(string Series, long ReturnedCount, double ReturnProbability, double MeanToReturn, double MedianToReturn);

	// Bin-free view of one run's scalar stats -- shared by the live DETAILS panel, Session cards,
	// and History cards, the last of which must stay cheap to list without fetching every row's bins.
	public sealed record AnalysisDetailView(
		ActiveAnalysis AnalysisType,
		string Label,
		DateTime? SavedAtUtc,
		TimeSpan? Elapsed,
		long SampleCount,
		string WindowText,
		double? MeanFractionAbove,
		double? MedianFractionAbove,
		double? StdDevFractionAbove,
		double? MinFractionAbove,
		double? MaxFractionAbove,
		long? ReturnedCount,
		double? ReturnProbability,
		double? MeanToReturn,
		double? MedianToReturn,
		string? ToReturnUnit,
		string? Mode,
		IReadOnlyList<AnalysisSeriesSummaryView>? SeriesBreakdown
	);

	public partial class AnalizePage : ComponentBase, IDisposable
	{
		private enum AnalizeMainTab
		{
			Chart,
			Results,
			Price,
		}

		private enum AnalizeLeftTab
		{
			Analyses,
			Session,
			History,
		}

		private sealed record TimeAboveBelowView(TimeAboveBelowRequest Request, TimeAboveBelowDistributionResult Result, TimeSpan? Elapsed);

		private sealed record ReturnView(PriceReturnRequest Request, PriceReturnDistributionResult Result, TimeSpan? Elapsed);

		private sealed record SessionEntry(
			Guid Id,
			ActiveAnalysis AnalysisType,
			string Label,
			DateTime RanAtUtc,
			TimeSpan? Elapsed,
			TimeAboveBelowRequest? TimeAboveBelowRequest,
			TimeAboveBelowDistributionResult? TimeAboveBelowResult,
			PriceReturnRequest? ReturnRequest,
			PriceReturnDistributionResult? ReturnResult,
			long? SavedHistoryId
		);

		[Inject]
		private ITimeAboveBelowAnalysisService TimeAboveBelowService { get; set; } = default!;

		[Inject]
		private IPriceReturnAnalysisService ReturnService { get; set; } = default!;

		[Inject]
		private IAnalysisHistoryRepository AnalysisHistoryRepository { get; set; } = default!;

		[Inject]
		private IBarKindChartService BarKindChartService { get; set; } = default!;

		[Inject]
		private IBarrierMapAnalysisService BarrierService { get; set; } = default!;

		[Inject]
		private ISymbolTableResolver SymbolTableResolver { get; set; } = default!;

		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Inject]
		private ILogger<AnalizePage> Logger { get; set; } = default!;

		private bool _leftPanelCollapsed;
		private bool _rightPanelCollapsed;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;

		private AnalizeMainTab _activeMainTab;
		private AnalizeLeftTab _activeLeftTab;
		private ActiveAnalysis _activeAnalysis;

		private readonly List<SessionEntry> _sessionEntries = new();
		private List<AnalysisHistoryRecord> _historyEntries = new();
		private TimeAboveBelowView? _pinnedTimeAboveBelow;
		private ReturnView? _pinnedReturn;
		private long? _pinnedHistoryId;
		private bool _timeAboveBelowWasRunning;
		private bool _returnWasRunning;
		private TimeAboveBelowRequest? _lastTimeAboveBelowRequest;
		private PriceReturnRequest? _lastReturnRequest;

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

		private string _barrierStopsText = "2, 5, 10, 15, 20, 30, 50";
		private string _barrierTargetsText = "2, 5, 10, 20, 30, 50, 100";
		private string _barrierLimitsText = "15, 60, 240";
		private int _barrierSpacingSeconds = 60;
		private double? _barrierPipSize;
		private string? _barrierPipSizeTable;
		private string? _barrierStartError;
		private bool _barrierCardCollapsed;
		private bool _barrierWasRunning;
		private BarrierMapSelection? _barrierSelection;

		protected override void OnInitialized()
		{
			TimeAboveBelowService.OnChanged += HandleServiceChanged;
			ReturnService.OnChanged += HandleServiceChanged;
			BarrierService.OnChanged += HandleBarrierChanged;
			_barrierWasRunning = BarrierService.IsRunning;
			_timeAboveBelowWasRunning = TimeAboveBelowService.IsRunning;
			_returnWasRunning = ReturnService.IsRunning;
			LoadHistory();
		}

		public void Dispose()
		{
			TimeAboveBelowService.OnChanged -= HandleServiceChanged;
			ReturnService.OnChanged -= HandleServiceChanged;
			BarrierService.OnChanged -= HandleBarrierChanged;
		}

		// Session cards only exist in this page's memory, so a completed run is captured here, the
		// one place both services' running-to-done transition is observed.
		private void HandleServiceChanged()
		{
			if (_timeAboveBelowWasRunning && !TimeAboveBelowService.IsRunning
				&& TimeAboveBelowService.Result is { SampleCount: > 0 } tabResult
				&& _lastTimeAboveBelowRequest is { } tabRequest)
			{
				_sessionEntries.Insert(
					0,
					new SessionEntry(
						Guid.NewGuid(),
						ActiveAnalysis.TimeAboveBelow,
						BuildAutoLabel(tabRequest.Table, tabRequest.From, tabRequest.To),
						DateTime.UtcNow,
						TimeAboveBelowService.Elapsed,
						tabRequest,
						tabResult,
						null,
						null,
						null
					)
				);
			}
			_timeAboveBelowWasRunning = TimeAboveBelowService.IsRunning;

			if (_returnWasRunning && !ReturnService.IsRunning
				&& ReturnService.Result is { SampleCount: > 0 } returnResult
				&& _lastReturnRequest is { } returnRequest)
			{
				_sessionEntries.Insert(
					0,
					new SessionEntry(
						Guid.NewGuid(),
						ActiveAnalysis.ReturnProbability,
						BuildAutoLabel(returnRequest.Table, returnRequest.From, returnRequest.To),
						DateTime.UtcNow,
						ReturnService.Elapsed,
						null,
						null,
						returnRequest,
						returnResult,
						null
					)
				);
			}
			_returnWasRunning = ReturnService.IsRunning;

			InvokeAsync(StateHasChanged);
		}

		private static string BuildAutoLabel(string table, DateTime from, DateTime to) =>
			$"{table} {from:yyyy-MM-dd HH:mm} to {to:yyyy-MM-dd HH:mm}";

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
			ClearPin();
			var request = new TimeAboveBelowRequest(_table, _from, _to, _timeAboveBelowWindowMinutes);
			if (TimeAboveBelowService.TryStart(request))
				_lastTimeAboveBelowRequest = request;
			else
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
			ClearPin();
			if (ReturnService.TryStart(request))
				_lastReturnRequest = request;
			else
				_returnStartError = "analysis is already running";
		}

		// Null pin => show whatever the live singleton service currently holds; a Session/History
		// selection pins a specific run so it survives the live service moving on to a new one.
		private TimeAboveBelowView? CurrentTimeAboveBelowView =>
			_pinnedTimeAboveBelow
			?? (TimeAboveBelowService.Result is { SampleCount: > 0 } result
				? new TimeAboveBelowView(new TimeAboveBelowRequest(_table, _from, _to, _timeAboveBelowWindowMinutes), result, TimeAboveBelowService.Elapsed)
				: null);

		private ReturnView? CurrentReturnView =>
			_pinnedReturn
			?? (ReturnService.Result is { SampleCount: > 0 } result ? new ReturnView(CurrentLiveReturnRequest(), result, ReturnService.Elapsed) : null);

		private PriceReturnRequest CurrentLiveReturnRequest() =>
			_returnUseAggregation
				? new PriceReturnRequest(_table, _from, _to, 0, _returnAggregationBarWidthSeconds, _returnWindowBars, _returnAggregationKind)
				: new PriceReturnRequest(_table, _from, _to, _returnWindowMinutes);

		private void ClearPin()
		{
			_pinnedTimeAboveBelow = null;
			_pinnedReturn = null;
			_pinnedHistoryId = null;
		}

		private AnalysisDetailView? CurrentDetailView =>
			_activeAnalysis == ActiveAnalysis.TimeAboveBelow
				? (CurrentTimeAboveBelowView is { } tabView ? BuildDetailView(tabView, "Details", null) : null)
				: (CurrentReturnView is { } returnView ? BuildDetailView(returnView, "Details", null) : null);

		private IReadOnlyList<HistogramBar> TimeAboveBelowChartBins =>
			CurrentTimeAboveBelowView is { } view
				? view.Result.Bins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList()
				: Array.Empty<HistogramBar>();

		private List<AnalizeBinRow> TimeAboveBelowResultRows =>
			CurrentTimeAboveBelowView is { } view
				? view
					.Result.Bins.Select(b => new AnalizeBinRow(
						FormatPercentBinRange(b),
						b.Count.ToString("N0"),
						(b.Count * 100.0 / view.Result.SampleCount).ToString("N1") + "%"
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
				if (CurrentReturnView is not { } view)
					return (Array.Empty<HistogramBin>(), 0, "min");
				var result = view.Result;
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
			CurrentReturnView?.Result.SeriesBreakdown is { } breakdown
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

		private static string FormatMinutesBinRange(HistogramBin bin) =>
			$"{bin.Low.ToString("0.#", CultureInfo.InvariantCulture)}-{bin.High.ToString("0.#", CultureInfo.InvariantCulture)}m";

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private string TimeAboveBelowProgressPercentText =>
			TimeAboveBelowService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private string ReturnProgressPercentText => ReturnService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private string ReturnWindowBarsMinutesText => (_returnAggregationBarWidthSeconds * _returnWindowBars / 60.0).ToString("N1");

		private string ReturnEffectiveWindowText =>
			_returnUseAggregation ? $"{_returnWindowBars} bars ({ReturnWindowBarsMinutesText} min)" : $"{_returnWindowMinutes} min";

		private static string FormatReturnWindowText(PriceReturnRequest r) => FormatReturnWindowText(r.AggregationBarWidthSeconds, r.AggregationWindowBars, r.WindowMinutes);

		private static string FormatReturnWindowText(int? aggregationBarWidthSeconds, int? aggregationWindowBars, double? windowMinutes) =>
			aggregationBarWidthSeconds is { } barSeconds && aggregationWindowBars is { } bars
				? $"{bars} bars ({(barSeconds * bars / 60.0).ToString("N1")} min)"
				: $"{windowMinutes} min";

		private static string FormatReturnModeText(PriceReturnRequest r) => FormatReturnModeText(r.AggregationBarWidthSeconds, r.AggregationKind.ToString());

		private static string FormatReturnModeText(int? aggregationBarWidthSeconds, string? aggregationKind) =>
			aggregationBarWidthSeconds is { } barSeconds ? $"{barSeconds}s {aggregationKind} bars" : "tick-level";

		private static AnalysisDetailView BuildDetailView(TimeAboveBelowView view, string label, DateTime? timestampUtc) =>
			new(
				ActiveAnalysis.TimeAboveBelow,
				label,
				timestampUtc,
				view.Elapsed,
				view.Result.SampleCount,
				$"{view.Request.WindowMinutes} min",
				view.Result.MeanFractionAbove,
				view.Result.MedianFractionAbove,
				view.Result.StdDevFractionAbove,
				view.Result.MinFractionAbove,
				view.Result.MaxFractionAbove,
				null,
				null,
				null,
				null,
				null,
				null,
				null
			);

		private static AnalysisDetailView BuildDetailView(ReturnView view, string label, DateTime? timestampUtc) =>
			new(
				ActiveAnalysis.ReturnProbability,
				label,
				timestampUtc,
				view.Elapsed,
				view.Result.SampleCount,
				FormatReturnWindowText(view.Request),
				null,
				null,
				null,
				null,
				null,
				view.Result.ReturnedCount,
				view.Result.ReturnProbability,
				view.Result.MeanToReturn,
				view.Result.MedianToReturn,
				view.Result.ToReturnUnit,
				FormatReturnModeText(view.Request),
				view.Result.SeriesBreakdown?.Select(s => new AnalysisSeriesSummaryView(s.Series, s.ReturnedCount, s.ReturnProbability, s.MeanToReturn, s.MedianToReturn)).ToList()
			);

		private static AnalysisDetailView BuildDetailView(SessionEntry entry) =>
			entry.AnalysisType == ActiveAnalysis.TimeAboveBelow
				? BuildDetailView(new TimeAboveBelowView(entry.TimeAboveBelowRequest!, entry.TimeAboveBelowResult!, entry.Elapsed), entry.Label, entry.RanAtUtc)
				: BuildDetailView(new ReturnView(entry.ReturnRequest!, entry.ReturnResult!, entry.Elapsed), entry.Label, entry.RanAtUtc);

		private static AnalysisDetailView BuildDetailView(AnalysisHistoryRecord record) =>
			record.AnalysisType == "TimeAboveBelow"
				? new AnalysisDetailView(
					ActiveAnalysis.TimeAboveBelow,
					record.Label,
					record.SavedAtUtc,
					null,
					record.SampleCount,
					$"{record.WindowMinutes} min",
					record.MeanFractionAbove,
					record.MedianFractionAbove,
					record.StdDevFractionAbove,
					record.MinFractionAbove,
					record.MaxFractionAbove,
					null,
					null,
					null,
					null,
					null,
					null,
					null
				)
				: new AnalysisDetailView(
					ActiveAnalysis.ReturnProbability,
					record.Label,
					record.SavedAtUtc,
					null,
					record.SampleCount,
					FormatReturnWindowText(record.AggregationBarWidthSeconds, record.AggregationWindowBars, record.WindowMinutes),
					null,
					null,
					null,
					null,
					null,
					record.ReturnedCount,
					record.ReturnProbability,
					record.MeanToReturn,
					record.MedianToReturn,
					record.ToReturnUnit,
					FormatReturnModeText(record.AggregationBarWidthSeconds, record.AggregationKind),
					record.SeriesBreakdown?.Select(s => new AnalysisSeriesSummaryView(s.Series, s.ReturnedCount, s.ReturnProbability, s.MeanToReturn, s.MedianToReturn)).ToList()
				);

		private void SaveSessionEntry(SessionEntry entry)
		{
			if (entry.SavedHistoryId is not null)
				return;

			var id =
				entry.AnalysisType == ActiveAnalysis.TimeAboveBelow
					? AnalysisHistoryRepository.SaveTimeAboveBelow(entry.Label, entry.TimeAboveBelowRequest!, entry.TimeAboveBelowResult!)
					: AnalysisHistoryRepository.SaveReturnProbability(entry.Label, entry.ReturnRequest!, entry.ReturnResult!);

			var index = _sessionEntries.IndexOf(entry);
			if (index >= 0)
				_sessionEntries[index] = entry with { SavedHistoryId = id };

			LoadHistory();
		}

		private void SelectSessionEntry(SessionEntry entry)
		{
			if (entry.AnalysisType == ActiveAnalysis.TimeAboveBelow)
				_pinnedTimeAboveBelow = new TimeAboveBelowView(entry.TimeAboveBelowRequest!, entry.TimeAboveBelowResult!, entry.Elapsed);
			else
				_pinnedReturn = new ReturnView(entry.ReturnRequest!, entry.ReturnResult!, entry.Elapsed);

			_pinnedHistoryId = entry.SavedHistoryId;
			_activeAnalysis = entry.AnalysisType;
			_activeMainTab = AnalizeMainTab.Chart;
		}

		private void LoadHistory() => _historyEntries = AnalysisHistoryRepository.ListHistory();

		// The only place a history entry's bins are fetched -- the History list itself stays cheap by
		// rendering straight off ListHistory()'s scalar-only rows.
		private void LoadHistoryEntry(long id)
		{
			var detail = AnalysisHistoryRepository.GetDetail(id);
			var record = detail.Record;

			if (record.AnalysisType == "TimeAboveBelow")
			{
				var request = new TimeAboveBelowRequest(record.TableName, record.FromUtc, record.ToUtc, record.WindowMinutes ?? 0);
				var result = new TimeAboveBelowDistributionResult(
					record.SampleCount,
					record.MeanFractionAbove ?? 0,
					record.MedianFractionAbove ?? 0,
					record.StdDevFractionAbove ?? 0,
					record.MinFractionAbove ?? 0,
					record.MaxFractionAbove ?? 0,
					detail.Bins.Select(b => new HistogramBin(b.Low, b.High, b.Count)).ToList()
				);
				_pinnedTimeAboveBelow = new TimeAboveBelowView(request, result, null);
				_activeAnalysis = ActiveAnalysis.TimeAboveBelow;
			}
			else
			{
				var aggregationKind = record.AggregationKind is { } kind ? Enum.Parse<BarAggregationKind>(kind) : BarAggregationKind.Ohlc;
				var request = new PriceReturnRequest(
					record.TableName,
					record.FromUtc,
					record.ToUtc,
					record.WindowMinutes ?? 0,
					record.AggregationBarWidthSeconds,
					record.AggregationWindowBars,
					aggregationKind
				);
				IReadOnlyList<HistogramBin> topBins = detail.Bins.Where(b => b.Series is null).Select(b => new HistogramBin(b.Low, b.High, b.Count)).ToList();
				var seriesBreakdown =
					record.SeriesBreakdown is { Count: > 0 } summaries
						? summaries
							.Select(s => new PriceReturnSeriesResult(
								s.Series,
								s.ReturnedCount,
								s.ReturnProbability,
								s.MeanToReturn,
								s.MedianToReturn,
								detail.Bins.Where(b => b.Series == s.Series).Select(b => new HistogramBin(b.Low, b.High, b.Count)).ToList()
							))
							.ToList()
						: null;
				var result = new PriceReturnDistributionResult(
					record.SampleCount,
					record.ReturnedCount ?? 0,
					record.ReturnProbability ?? 0,
					record.MeanToReturn ?? 0,
					record.MedianToReturn ?? 0,
					topBins,
					record.ToReturnUnit ?? "min",
					seriesBreakdown
				);
				_pinnedReturn = new ReturnView(request, result, null);
				_activeAnalysis = ActiveAnalysis.ReturnProbability;
			}

			_pinnedHistoryId = id;
			_activeMainTab = AnalizeMainTab.Chart;
		}

		private void DeleteHistoryEntry(long id)
		{
			AnalysisHistoryRepository.DeleteHistory(id);
			if (_pinnedHistoryId == id)
				ClearPin();
			LoadHistory();
		}

		private void HandleBarrierChanged()
		{
			if (_barrierWasRunning && !BarrierService.IsRunning && BarrierService.Result is { } result)
			{
				_barrierSelection = new BarrierMapSelection(BarrierSide.Both, result.Request.TimeLimitMinutes[0], BarrierMetric.MeanPips, null);
				_activeAnalysis = ActiveAnalysis.BarrierMap;
			}
			_barrierWasRunning = BarrierService.IsRunning;
			InvokeAsync(StateHasChanged);
		}

		private async Task RunBarrierMap()
		{
			_barrierStartError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_barrierStartError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_barrierStartError = "To must be after From";
				return;
			}
			var stops = ParsePositiveNumbers(_barrierStopsText);
			var targets = ParsePositiveNumbers(_barrierTargetsText);
			var limits = ParsePositiveNumbers(_barrierLimitsText).Select(m => (int)Math.Round(m)).Where(m => m > 0).ToList();
			if (stops.Count == 0 || targets.Count == 0 || limits.Count == 0)
			{
				_barrierStartError = "enter at least one stop loss, take profit and time limit";
				return;
			}
			if (_barrierSpacingSeconds <= 0)
			{
				_barrierStartError = "entry spacing must be greater than 0";
				return;
			}

			await EnsureBarrierPipSizeAsync();
			if (_barrierPipSize is not > 0)
			{
				_barrierStartError = "pip size is unknown -- enter it";
				return;
			}

			_activeAnalysis = ActiveAnalysis.BarrierMap;
			ClearPin();
			var request = new BarrierMapRequest(_table, _from, _to, _barrierPipSize.Value, stops, targets, limits, _barrierSpacingSeconds);
			if (BarrierService.TryStart(request))
				_barrierWasRunning = true;
			else
				_barrierStartError = "analysis is already running";
		}

		// The pip size follows the symbol, so it is looked up again whenever the table has changed since the last lookup.
		private async Task EnsureBarrierPipSizeAsync()
		{
			if (_barrierPipSizeTable == _table && _barrierPipSize is > 0)
				return;
			try
			{
				var resolution = await SymbolTableResolver.ResolveAsync(_table);
				if (resolution.Limits is { } limits)
					_barrierPipSize = limits.PipSize;
				_barrierPipSizeTable = _table;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Could not resolve the pip size for table {Table}", _table);
			}
		}

		private static List<double> ParsePositiveNumbers(string text) =>
			text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0)
				.Where(value => value > 0)
				.ToList();

		private string BarrierProgressPercentText => BarrierService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);

		private List<BarrierResultRow> BarrierResultRows =>
			BarrierService.Result is { } result
			&& _barrierSelection is { } selection
			&& result.Grid(selection.Side, selection.TimeLimitMinutes) is { } grid
				? grid
					.Cells.Select(cell => new BarrierResultRow(
						cell.StopPips.ToString("0.#"),
						cell.TargetPips.ToString("0.#"),
						cell.Trades.ToString("N0"),
						cell.Wins.ToString("N0"),
						cell.Losses.ToString("N0"),
						cell.Timeouts.ToString("N0"),
						cell.WinRate?.ToString("P1") ?? "--",
						cell.BenchmarkWinRate?.ToString("P1") ?? "--",
						cell.Edge is { } edge ? (edge * 100).ToString("+0.0;-0.0") + "%" : "--",
						cell.MeanPips.ToString("+0.00;-0.00"),
						cell.BenchmarkMeanPips?.ToString("+0.00;-0.00") ?? "--",
						cell.EdgePips?.ToString("+0.00;-0.00") ?? "--",
						cell.CiPips?.ToString("0.00") ?? "--",
						cell.MeanMinutes.ToString("0.0")
					))
					.ToList()
				: new List<BarrierResultRow>();

		private async Task ExportBarrierCsvAsync()
		{
			if (BarrierService.Result is not { } result)
				return;
			try
			{
				await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/fileDownload.js");
				var fileName = $"barrier-map-{result.Request.Table}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
				await module.InvokeVoidAsync("downloadText", fileName, BarrierMapCsv.Build(result), "text/csv");
			}
			catch (Exception ex)
			{
				_barrierStartError = $"export failed: {ex.Message}";
				Logger.LogError(ex, "Could not export the barrier map");
			}
		}

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
				var (bars, medianLine) = await BarKindChartService.BuildBarsAsync(table, from, to, barWidthSeconds, barKind);
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
	}
}
