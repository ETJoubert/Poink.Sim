using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Shared.PositionTable;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
	public partial class SimulationPage : ComponentBase, IDisposable, IAsyncDisposable
	{
		[Inject]
		private ISimulationEngine SimulationService { get; set; } = default!;

		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Inject]
		private IRiskPolicyService RiskPolicyService { get; set; } = default!;

		[Inject]
		private IProfilerService ProfilerService { get; set; } = default!;

		[Inject]
		private ILogger<SimulationPage> Logger { get; set; } = default!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		// ── Setup form state ──

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;
		private bool _aggregate = true;
		private int _barSizeSeconds = 60;

		private double _startingCash = 10_000;
		private int _traderCount = 1;

		// ── Trader (CFD account) controls ──
		private double _leverage = 1;
		private double _commissionRatePercent = 0;
		private double _stopOutLevelPercent = 50;
		private double _pipSize = 0.01;

		private readonly List<string> _selectedRiskPolicies = new();

		private string? _loadError;

		// ── Live state (mirrors the service) ──

		private SimulationSnapshot? _snapshot;
		private bool _isLoading;
		private bool _playOnLoad;
		private TradeConsoleOptions? _robotPositionOptions;
		private RobotRuleSet _openLongRules = new();
		private RobotRuleSet _openShortRules = new();
		private RobotRuleSet _closeRules = new();
		private bool _robotEnabled;
		private double _robotEvaluationSeconds = 1;
		private RobotSubTab _robotSubTab;
		private RobotOpenSide _robotOpenSide;
		private TradeConsoleOptions? _tradeConsoleOptions;
		private string? _error;
		private bool _hideChart;

		// Fits the chart once on a fresh run, then latches true so live ticks preserve the
		// user's own pan/zoom instead of refitting every tick. See MarketChart.KeepView.
		private bool _keepChartView;

		// Bars scrolled in from the tick repository, older than the engine's own live RecentBars
		// tail -- cleared on config change or cursor rewind, see SyncHistoryState.
		private const int HistoryBarsPerFetch = 300;
		private SimulationConfig? _historyConfig;
		private List<HlocAvdtBar> _historyBars = new();
		private bool _historyReachedStart;
		private bool _isLoadingHistory;
		private int _lastHistoryCursorIndex;

		private bool _setupCollapsed;
		private int? _selectedTraderId;

		// Matches SimulationEngine.MaxRecentTicks.
		private const int MaxTickWindow = 5000;
		private int _tickWindowSize = 200;
		private int ClampedTickWindow => Math.Clamp(_tickWindowSize, 2, MaxTickWindow);

		// Ticks back from the most recent tick to the end of the window -- unlike Profiler's
		// Offset, this can never reach further back than the live RecentTicks ring buffer holds.
		private int _tickWindowOffset;
		private int ClampedTickOffset => Math.Clamp(_tickWindowOffset, 0, MaxTickWindow - 1);

		// Matches SimulationEngine.MaxRecentBars.
		private const int MaxBarWindow = 300;
		private int _barWindowSize = 50;
		private int ClampedBarWindow => Math.Clamp(_barWindowSize, 1, MaxBarWindow);

		// Bars back from the most recent bar to the end of the window -- same idea as Profiler's
		// Offset, but bounded by the live RecentBars ring buffer instead of arbitrary loaded history.
		private int _barWindowOffset;
		private int ClampedBarOffset => Math.Clamp(_barWindowOffset, 0, MaxBarWindow - 1);

		// Slices RecentBars/RecentBarHistograms to end Offset bars back from the live edge --
		// every width-based window below reads from this so Offset moves them all together.
		private (IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> Histograms) WindowedBarData(
			SimulationSnapshot s
		)
		{
			var offset = Math.Min(ClampedBarOffset, Math.Max(0, s.RecentBars.Count - 1));
			var count = s.RecentBars.Count - offset;
			if (count <= 0)
				return (Array.Empty<HlocAvdtBar>(), Array.Empty<BarPriceHistogram>());
			return (s.RecentBars.Take(count).ToList(), s.RecentBarHistograms.Take(count).ToList());
		}

		// Tick-mode equivalent of WindowedBarData.
		private IReadOnlyList<Tick> WindowedTicks(SimulationSnapshot s)
		{
			var offset = Math.Min(ClampedTickOffset, Math.Max(0, s.RecentTicks.Count - 1));
			var count = s.RecentTicks.Count - offset;
			return count <= 0 ? Array.Empty<Tick>() : s.RecentTicks.Take(count).ToList();
		}

		private bool _showProfilePanelLevels = true;
		private bool _showProfileChartLines = true;
		private bool _showVwap = true;

		private bool _showMainChart = true;
		private bool _showXGrid;
		private bool _showVolumeProfile;
		private bool _showTimeProfile;
		private bool _showSnapshot;
		private bool _traderControlsCollapsed;
		private bool _leftSidePanelCollapsed;
		private SimLeftPanelTab _activeLeftPanelTab;

		private enum SimLeftPanelTab
		{
			Setup,
			Traders,
			RuleEngine,
		}

		// Above/In/Below mask the Rule Engine tab edits -- keyed by RuleEngineHelper key,
		// owned by the page so it survives tab switches.
		private HashSet<string> _ruleEngineEnabledKeys = new();
		private double _ruleEngineXGridPercent = 10;
		private HashSet<RuleSignalGroup> _ruleEngineDisabledGroups = new();

		// Shared with the Snapshot tab's own TxFold chart so both evaluate/display the same timeframes.
		private HashSet<int> _txFoldSelectedSeconds = new() { 60, 120, 240, 480, 900, 1800, 3600, 7200 };

		private int _xgridPipCount = 20;
		private int _xgridColCount = 20;

		// A divider's negative margin assumes a panel to its left, so a divider only shows
		// once an earlier panel (main -> XGrid -> volume profile -> time profile) is visible.
		private bool ShowXGridDivider(SimulationSnapshot s) => s.Config.Aggregate && _showXGrid && _showMainChart;

		private bool ShowVolumeProfileDivider(SimulationSnapshot s) =>
			_showVolumeProfile && (_showMainChart || (s.Config.Aggregate && _showXGrid));

		private bool ShowTimeProfileDivider(SimulationSnapshot s) =>
			_showTimeProfile && (_showMainChart || (s.Config.Aggregate && _showXGrid) || _showVolumeProfile);

		private bool ShowSnapshotDivider(SimulationSnapshot s) =>
			_showSnapshot && (_showMainChart || (s.Config.Aggregate && _showXGrid) || _showVolumeProfile || _showTimeProfile);

		// The live window's rightmost bar -- CloseTime/Close (not OpenTime) so Snapshot's tick-level
		// query reaches through the whole bar instead of stopping at its start.
		private HlocAvdtBar? SnapshotAnchorBar(SimulationSnapshot s)
		{
			var (bars, _) = WindowedBarData(s);
			return bars.Count > 0 ? bars[^1] : null;
		}

		private XGridResult? _xgridCache;
		private (
			int Pips,
			double PipSize,
			int Cols,
			int Offset,
			int Window,
			int Count,
			DateTime LastOpen,
			double LastClose,
			double LastVolume
		)? _xgridKey;

		// Row height comes from the account's own pip size (Config.PipSize, set up before
		// load, same value TradeConsole uses for SL/TP), not a value XGrid owns itself.
		private XGridResult? ComputeXGrid(SimulationSnapshot s)
		{
			if (!s.Config.Aggregate)
				return null;
			var (bars, histograms) = WindowedBarData(s);
			if (bars.Count == 0)
				return null;
			var window = Math.Min(ClampedBarWindow, bars.Count);
			var last = bars[^1];
			var key = (
				_xgridPipCount,
				s.Config.PipSize,
				_xgridColCount,
				ClampedBarOffset,
				window,
				bars.Count,
				last.OpenTime,
				last.Close,
				(double)last.Volume
			);
			if (_xgridKey == key && _xgridCache is not null)
				return _xgridCache;

			var start = bars[bars.Count - window].OpenTime;
			var end = last.OpenTime;
			_xgridKey = key;
			_xgridCache = XGridStats.Compute(
				bars,
				histograms,
				MergedBarSamples(s),
				start,
				end,
				_xgridPipCount * s.Config.PipSize,
				_xgridColCount
			);
			return _xgridCache;
		}

		// Baseline signals come straight off RangeStatsHelper; the XGrid rank/delta rows ride along
		// on top by handing RuleEngineHelper the same grid ComputeXGrid builds for the chart itself.
		private RuleEngineResult? ComputeRuleEngine(SimulationSnapshot s, double xgridPercent, HashSet<RuleSignalGroup> disabledGroups)
		{
			var (bars, _) = WindowedBarData(s);
			if (bars.Count == 0 || s.CurrentPrice is not { } currentPrice)
				return null;

			bool baseline = !disabledGroups.Contains(RuleSignalGroup.Baseline);
			bool xgrid = !disabledGroups.Contains(RuleSignalGroup.XGrid);
			return RuleEngineHelper.Compute(
				baseline || xgrid ? MergedBarSamples(s) : new List<WeightedPriceSample>(),
				w => w.Price,
				w => (double)w.Count,
				bars,
				b => b.Average,
				b => (double)b.Volume,
				b => b.Close,
				currentPrice,
				xgrid ? ComputeXGrid(s) : null,
				xgridPercent,
				baseline
			);
		}

		// Recomputed each render so live P&L colouring on the open position stays current;
		// trades entirely outside the grid's window are dropped to keep the overlay uncluttered.
		private List<XGridPosition> XGridPositions(SimulationSnapshot s)
		{
			var result = new List<XGridPosition>();
			if (SelectedTrader is not { } trader)
				return result;

			var (bars, _) = WindowedBarData(s);
			var window = Math.Min(ClampedBarWindow, bars.Count);
			if (window == 0)
				return result;
			var start = bars[bars.Count - window].OpenTime;
			var end = bars[^1].OpenTime;

			foreach (var trade in trader.Trades)
			{
				if (trade.ExitTime < start || trade.EntryTime > end)
					continue;
				result.Add(
					new XGridPosition(
						MapDirection(trade.Direction),
						trade.EntryTime,
						trade.EntryPrice,
						trade.ExitTime,
						trade.ExitPrice
					)
				);
			}
			if (trader.Position is { } pos)
				result.Add(
					new XGridPosition(
						MapDirection(pos.Direction),
						pos.EntryTime,
						pos.EntryPrice,
						StopLoss: pos.StopLossPrice,
						TakeProfit: pos.TakeProfitPrice
					)
					{
						Id = "position",
					}
				);
			return result;
		}

		private static XGridPositionDirection MapDirection(PositionDirection direction) =>
			direction == PositionDirection.Long ? XGridPositionDirection.Long : XGridPositionDirection.Short;

		private List<XGridPending> XGridPending()
		{
			var result = new List<XGridPending>();
			if (SelectedTrader?.PendingOrder is { } pending)
				result.Add(
					new XGridPending(
						pending.Direction == PositionDirection.Long,
						pending.TriggerPrice,
						pending.CapitalFraction,
						pending.StopLossPrice,
						pending.TakeProfitPrice
					)
					{
						Id = "pending",
					}
				);
			return result;
		}

		private void CancelXGridPending(string id) => CloseSelectedTrader("xgrid-cancel");

		private void CloseXGridPosition(string id) => CloseSelectedTrader("xgrid-close");

		// Cancel & re-place: the engine has no in-place modify, so the resting order is closed
		// and a fresh one submitted with the edited values.
		private void ModifyXGridPending(XGridOrderModification modification)
		{
			CloseSelectedTrader("xgrid-modify");
			PlaceXGridOrder(
				new XGridOrder(
					modification.IsLong,
					modification.TriggerPrice,
					modification.Volume,
					modification.StopLoss,
					modification.TakeProfit
				)
			);
		}

		private void CloseSelectedTrader(string reason)
		{
			if (SelectedTrader is not { } trader)
				return;
			_consoleError = null;
			if (
				!SimulationService.SubmitManualDecision(
					trader.Id,
					new SimulationDecision(SimulationAction.Close, Reason: reason),
					out RiskPolicyDenial? denial
				)
			)
				_consoleError = "close rejected -- no open position or resting order";
			else if (denial is { } d)
				_consoleError = $"denied by {d.PolicyName}: {d.Reason}";
		}

		// Entry price becomes a Limit trigger when it sits on the favourable side of current
		// price, a Stop otherwise; Volume maps onto the engine's capital fraction.
		private void PlaceXGridOrder(XGridOrder order)
		{
			if (SelectedTrader is not { } trader)
				return;

			double current = _snapshot?.CurrentPrice ?? order.EntryPrice;
			var action = order.IsLong ? SimulationAction.EnterLong : SimulationAction.EnterShort;
			OrderType orderType = order.IsLong
				? (order.EntryPrice <= current ? OrderType.Limit : OrderType.Stop)
				: (order.EntryPrice >= current ? OrderType.Limit : OrderType.Stop);
			double fraction = Math.Clamp(order.Volume, 0.01, 1.0);

			var decision = new SimulationDecision(
				action,
				fraction,
				order.StopLoss,
				order.TakeProfit,
				orderType,
				order.EntryPrice,
				Reason: "xgrid"
			);
			SimulationService.SubmitManualDecision(trader.Id, decision, out _);
		}

		private string? _consoleError;
		private TradeConsole? _consoleRef;

		/// <summary>The console's live not-yet-submitted order, previewed on the Snapshot chart and XGrid while TradeConsole's PREVIEW toggle is active.</summary>
		private TradeConsoleDraft? _draft;

		private void HandleDraftChanged(TradeConsoleDraft? draft) => _draft = draft;

		private XGridExternalDraft? XGridDraft =>
			_draft is { } d ? new XGridExternalDraft(d.EntryPrice, d.IsLong, d.StopLossPrice, d.TakeProfitPrice) : null;

		private void HandleConsoleSubmit(TradeConsoleOrderRequest request)
		{
			_consoleError = null;
			if (SelectedTrader is not { } trader)
			{
				_consoleError = "no trader selected";
				return;
			}

			double? entry = request.OrderType == OrderType.Market
				? (request.IsLong ? _snapshot?.CurrentAsk : _snapshot?.CurrentBid) ?? _snapshot?.CurrentPrice
				: request.TriggerPrice;
			if (entry is not { } entryPrice || entryPrice <= 0 || trader.Capital.Cash <= 0)
			{
				_consoleError = "no market price available yet";
				return;
			}

			double capitalFraction = RiskHelper.MarginRequired(request.Volume, entryPrice, trader.Leverage) / trader.Capital.Cash;
			if (capitalFraction is <= 0 or > 1)
			{
				_consoleError = "volume must resolve to between 0 and 100% of cash";
				return;
			}

			var action = request.IsLong ? SimulationAction.EnterLong : SimulationAction.EnterShort;
			var orderType = request.OrderType == OrderType.Market ? (OrderType?)null : request.OrderType;
			var decision = new SimulationDecision(
				action,
				capitalFraction,
				request.StopLossPrice,
				request.TakeProfitPrice,
				orderType,
				request.TriggerPrice,
				Reason: "manual"
			);

			if (!SimulationService.SubmitManualDecision(trader.Id, decision, out RiskPolicyDenial? denial))
				_consoleError = "order rejected -- already in a position or an order is already resting";
			else if (denial is { } d)
				_consoleError = $"denied by {d.PolicyName}: {d.Reason}";
			else
				_consoleRef?.ClearDraftPreview();
		}

		private List<WeightedPriceSample> MergedBarSamples(SimulationSnapshot s)
		{
			var (_, histograms) = WindowedBarData(s);
			return BarPriceHistogram
				.Merge(histograms, ClampedBarWindow)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
		}

		private List<WeightedTimeSample> MergedBarTimeSamples(SimulationSnapshot s)
		{
			var (bars, histograms) = WindowedBarData(s);
			return BarPriceHistogram
				.MergeTime(bars, histograms, ClampedBarWindow)
				.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
				.ToList();
		}

		private List<ChartHorizontalLine> ProfileLevelLines(SimulationSnapshot s)
		{
			if (!_showProfileChartLines)
				return new();
			if (VolumeProfileStats.Compute(MergedBarSamples(s)) is not { } levels)
				return new();
			return new()
			{
				new ChartHorizontalLine(levels.Poc, "POC", "profile"),
				new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
				new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
			};
		}

		private List<ChartLinePoint> VwapPoints(SimulationSnapshot s)
		{
			if (!_showVwap)
				return new();

			var (bars, _) = WindowedBarData(s);
			var windowBars = bars.Count > ClampedBarWindow ? bars.Skip(bars.Count - ClampedBarWindow) : bars;

			return VectorStatsHelper
				.Vwap(windowBars, b => b.OpenTime, b => b.Average, b => b.Volume)
				.Select(p => new ChartLinePoint(p.TimeUtc, p.Value))
				.ToList();
		}

		private DateTime? WindowStartUtc(SimulationSnapshot s)
		{
			var (bars, _) = WindowedBarData(s);
			if (bars.Count <= ClampedBarWindow)
				return null;
			return bars[bars.Count - ClampedBarWindow].OpenTime;
		}

		// Only meaningful once Offset pulls the window's far edge off of "now" -- at Offset 0
		// the end is just the newest bar, already the chart's own right edge.
		private DateTime? WindowEndUtc(SimulationSnapshot s)
		{
			if (ClampedBarOffset <= 0)
				return null;
			var (bars, _) = WindowedBarData(s);
			return bars.Count > 0 ? bars[^1].OpenTime : null;
		}

		private TraderSnapshot? SelectedTrader =>
			_snapshot is null
				? null
				: _snapshot.Traders.FirstOrDefault(t => t.Id == _selectedTraderId)
					?? _snapshot.Traders.FirstOrDefault();

		private void SelectTrader(int id)
		{
			_selectedTraderId = id;
		}

		private static string TraderPositionStatus(TraderSnapshot trader) =>
			trader.Position is { } position ? (position.Direction == PositionDirection.Long ? "LONG" : "SHORT")
			: trader.PendingOrder is not null ? "PENDING"
			: "FLAT";

		private static string TraderPositionStatusCssClass(TraderSnapshot trader) =>
			trader.Position is { } position ? (position.Direction == PositionDirection.Long ? "tcard-status-long" : "tcard-status-short")
			: trader.PendingOrder is not null ? "tcard-status-pending"
			: "tcard-status-flat";

		private bool? SelectedPositionIsLong =>
			SelectedTrader?.Position?.Direction switch
			{
				PositionDirection.Long => true,
				PositionDirection.Short => false,
				null => null,
				_ => null,
			};

		private List<EquityCurveSample> SelectedTraderEquityCurveSamples =>
			SelectedTrader is { } trader
				? trader
					.TradeEquity.Select(p => new EquityCurveSample(p.TimestampUtc, p.Equity, p.MaxEquity, p.MinEquity, p.TradeNumber))
					.ToList()
				: [];

		// ── Lifecycle ──

		protected override void OnInitialized()
		{
			SimulationService.OnChanged += HandleServiceChanged;
			ApplyServiceState();
			_selectedRiskPolicies.AddRange(RiskPolicyService.AvailablePolicies.Select(p => p.Name));
		}

		// Start is load-then-play in one click; the load runs in the background, so playback begins
		// once the engine reports it finished loading.
		private void PlayWhenLoadSettles()
		{
			if (!_playOnLoad || _isLoading)
				return;
			_playOnLoad = false;
			if (_snapshot is not null)
				SimulationService.Play();
		}

		private void HandleServiceChanged() =>
			_ = InvokeAsync(() =>
			{
				ApplyServiceState();
				StateHasChanged();
			});

		private void ApplyServiceState()
		{
			_snapshot = SimulationService.Snapshot;
			_isLoading = SimulationService.IsLoading;
			_error = SimulationService.Error;
			SyncHistoryState(_snapshot);
			PlayWhenLoadSettles();
		}

		// A config change invalidates already-fetched history; a cursor moving backward means
		// Reset/Seek replayed from scratch, so bars once "older than now" may no longer be.
		private void SyncHistoryState(SimulationSnapshot? snapshot)
		{
			if (snapshot is null || snapshot.Config != _historyConfig || snapshot.CursorIndex < _lastHistoryCursorIndex)
			{
				_historyBars = new();
				_historyReachedStart = false;
				_historyConfig = snapshot?.Config;
			}
			_lastHistoryCursorIndex = snapshot?.CursorIndex ?? 0;
		}

		// Deduped by OpenTime, keeping the later (live) copy of any bar that overlaps the
		// history/live boundary -- lightweight-charts requires strictly unique ascending times.
		private IReadOnlyList<HlocAvdtBar> ChartBars(SimulationSnapshot s)
		{
			if (_historyBars.Count == 0)
				return s.RecentBars;
			return _historyBars
				.Concat(s.RecentBars)
				.GroupBy(b => b.OpenTime)
				.Select(g => g.Last())
				.OrderBy(b => b.OpenTime)
				.ToList();
		}

		// MarketChart's OnNearStart -- pulls an older chunk straight from the tick repository,
		// independent of the engine's own live RecentBars ring buffer.
		private async Task LoadOlderChartHistory()
		{
			if (_snapshot is null || _historyReachedStart || _isLoadingHistory)
				return;

			var config = _snapshot.Config;
			var oldestKnown =
				_historyBars.Count > 0 ? _historyBars[0].OpenTime
				: _snapshot.RecentBars.Count > 0 ? _snapshot.RecentBars[0].OpenTime
				: config.To;
			if (oldestKnown <= config.From)
			{
				_historyReachedStart = true;
				return;
			}

			_isLoadingHistory = true;
			try
			{
				var profilerConfig = new ProfilerConfig(config.Table, config.From, config.To, config.BarSize);
				var chunk = await ProfilerService.LoadAsync(profilerConfig, oldestKnown, HistoryBarsPerFetch);
				_historyBars = chunk.Bars.Concat(_historyBars).ToList();
				_historyReachedStart = chunk.ReachedStart;
				_historyConfig = config;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Failed to backfill simulation chart history for table {Table}", config.Table);
			}
			finally
			{
				_isLoadingHistory = false;
			}
			StateHasChanged();
		}

		public void Dispose()
		{
			SimulationService.OnChanged -= HandleServiceChanged;
		}

		// Round-trips the setup form through localStorage since this component's own fields
		// don't survive navigation; diffed against _lastPersistedSettingsJson to avoid write spam.
		private const string SettingsStorageKey = "poink.simulation.setup";

		private string? _lastPersistedSettingsJson;
		private bool _settingsLoaded;

		private SimulationPageSettings CurrentSettings =>
			new(
				_table,
				_from,
				_to,
				_aggregate,
				_barSizeSeconds,
				_startingCash,
				_traderCount,
				_leverage,
				_commissionRatePercent,
				_stopOutLevelPercent,
				_robotPositionOptions,
				_openLongRules,
				_openShortRules,
				_closeRules,
				_robotEnabled,
				_tradeConsoleOptions,
				_robotSubTab,
				_robotOpenSide,
				_robotEvaluationSeconds
			);

		// A simulation already held by the service (page revisited) is shown as-is instead of reloaded.
		private async Task LoadInitialSimulation()
		{
			if (_snapshot is null && !_isLoading)
				await LoadSimulationAsync(playWhenLoaded: false);
		}

		private async Task LoadPersistedSettingsAsync()
		{
			try
			{
				var settings = await LocalStorage.GetItemAsync<SimulationPageSettings>(SettingsStorageKey);
				if (settings is null)
					return;

				ApplySettings(settings);
				_lastPersistedSettingsJson = JsonSerializer.Serialize(CurrentSettings);
				StateHasChanged();
			}
			finally
			{
				_settingsLoaded = true;
			}
		}

		private void ApplySettings(SimulationPageSettings settings)
		{
			_table = settings.Table;
			_from = settings.From;
			_to = settings.To;
			_aggregate = settings.Aggregate;
			_barSizeSeconds = settings.BarSizeSeconds;
			_startingCash = settings.StartingCash;
			_traderCount = settings.TraderCount;
			_leverage = settings.Leverage;
			_commissionRatePercent = settings.CommissionRatePercent;
			_stopOutLevelPercent = settings.StopOutLevelPercent;
			_robotPositionOptions = settings.RobotPositionOptions;
			_openLongRules = settings.OpenLongRules ?? new();
			_openShortRules = settings.OpenShortRules ?? new();
			_closeRules = settings.CloseRules ?? new();
			_robotEnabled = settings.RobotEnabled;
			_tradeConsoleOptions = settings.TradeOptions;
			_robotSubTab = settings.RobotSubTab;
			_robotOpenSide = settings.RobotOpenSide;
			_robotEvaluationSeconds = settings.RobotEvaluationSeconds ?? 1;
		}

		// Saving before the stored settings were read would overwrite them with the page's defaults.
		private async Task PersistSettingsIfChangedAsync()
		{
			if (!_settingsLoaded)
				return;

			var json = JsonSerializer.Serialize(CurrentSettings);
			if (json == _lastPersistedSettingsJson)
				return;

			_lastPersistedSettingsJson = json;
			await LocalStorage.SetItemAsync(SettingsStorageKey, CurrentSettings);
		}

		private ElementReference _chartRowRef;
		private ElementReference _chartDividerRefXGrid;
		private ElementReference _chartDividerRef;
		private ElementReference _chartDividerRef2;
		private ElementReference _chartDividerRefSnapshot;
		private ElementReference _chartHDividerRef;
		private IJSObjectReference? _dividerResizeInstance;
		private HashSet<string> _lastVisibleChartPanels = new();

		private const string ChartDividerScript = """
			(function() {
			    // Every .sim-chart-main/.sim-chart-side child gets an explicit flex-basis
			    // percent (not width px) so N panels always sum to 100% regardless of window
			    // size -- resetChartPanelsEqual splits it evenly, bindChartDivider's drag only
			    // ever moves percent between the divider's own two immediate neighbours.
			    // Net px a divider adds beyond its neighbours' own width: two 16px gaps narrowed
			    // by its -10px margins, plus its own 6px -- keep in sync with .sim-chart's CSS.
			    var DIVIDER_CHROME_PX = 18;
			    function resetChartPanelsEqual(container) {
			        if (!container) return;
			        var panels = container.querySelectorAll(':scope > .sim-chart-main, :scope > .sim-chart-side');
			        if (panels.length === 0) return;
			        var dividerCount = container.querySelectorAll(':scope > .sim-chart-divider').length;
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
			            // flex-basis % resolves against the content box, not the border box
			            // getBoundingClientRect returns -- subtract padding or drags drift right-ward.
			            var containerStyle = getComputedStyle(container);
			            var containerWidth = container.getBoundingClientRect().width
			                - (parseFloat(containerStyle.paddingLeft) || 0)
			                - (parseFloat(containerStyle.paddingRight) || 0);
			            var minPercent = (minWidthPx / containerWidth) * 100;
			            var startX = e.clientX;
			            var startLeftPercent = (left.getBoundingClientRect().width / containerWidth) * 100;
			            var startRightPercent = (right.getBoundingClientRect().width / containerWidth) * 100;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('sim-chart-divider-active');

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
			                divider.classList.remove('sim-chart-divider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    function bindHeightAfter(divider) {
			        if (!divider || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minHeight = parseInt(divider.dataset.minHeight, 10) || 160;
			        var maxHeightVh = parseInt(divider.dataset.maxHeightVh, 10) || 80;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var target = divider.nextElementSibling;
			            if (!target) return;
			            var startY = e.clientY;
			            var startHeight = target.getBoundingClientRect().height;
			            var maxHeight = (maxHeightVh / 100) * window.innerHeight;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('sim-chart-hdivider-active');

			            function clamp(ev) {
			                return Math.min(maxHeight, Math.max(minHeight, startHeight - (ev.clientY - startY)));
			            }
			            function onMove(ev) {
			                target.style.height = clamp(ev) + 'px';
			            }
			            function onUp(ev) {
			                divider.releasePointerCapture(e.pointerId);
			                divider.removeEventListener('pointermove', onMove);
			                divider.removeEventListener('pointerup', onUp);
			                divider.classList.remove('sim-chart-hdivider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    return {
			        bindChartDivider: bindChartDivider, bindHeightAfter: bindHeightAfter,
			        resetChartPanelsEqual: resetChartPanelsEqual,
			    };
			})()
			""";

		private HashSet<string> CurrentVisibleChartPanelKeys(SimulationSnapshot s)
		{
			var keys = new HashSet<string>();
			if (_showMainChart) keys.Add("main");
			if (s.Config.Aggregate && _showXGrid) keys.Add("xgrid");
			if (_showVolumeProfile) keys.Add("volume");
			if (_showTimeProfile) keys.Add("time");
			if (_showSnapshot) keys.Add("snapshot");
			return keys;
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartDividerScript);
				await LoadPersistedSettingsAsync();
				await LoadInitialSimulation();
			}
			else
			{
				await PersistSettingsIfChangedAsync();
			}

			PushRobotOptionsIfChanged();
			await RefreshSymbolMetadataIfTableChangedAsync();

			if (_snapshot is not null && !_hideChart && _dividerResizeInstance is not null)
			{
				var visibleChartPanels = CurrentVisibleChartPanelKeys(_snapshot);
				if (!visibleChartPanels.SetEquals(_lastVisibleChartPanels))
				{
					_lastVisibleChartPanels = visibleChartPanels;
					await _dividerResizeInstance.InvokeVoidAsync("resetChartPanelsEqual", _chartRowRef);
				}

				if (ShowXGridDivider(_snapshot))
					await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRefXGrid);
				if (ShowVolumeProfileDivider(_snapshot))
					await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRef);
				if (ShowTimeProfileDivider(_snapshot))
					await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRef2);
				if (ShowSnapshotDivider(_snapshot))
					await _dividerResizeInstance.InvokeVoidAsync("bindChartDivider", _chartDividerRefSnapshot);
				await _dividerResizeInstance.InvokeVoidAsync("bindHeightAfter", _chartHDividerRef);
			}

			if (
				!_keepChartView
				&& _snapshot is { Config.Aggregate: true } snap
				&& !_hideChart
				&& snap.RecentBars.Count > 0
			)
				_keepChartView = true;
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

		// ── Setup actions ──

		// The symbol's pip size feeds the account, so its metadata is resolved before every load.
		private async Task LoadSimulationAsync(bool playWhenLoaded)
		{
			await EnsureSymbolMetadataAsync();
			LoadSimulation(playWhenLoaded);
		}

		private void LoadSimulation(bool playWhenLoaded)
		{
			_loadError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_loadError = "pick a symbol";
				return;
			}
			if (_symbolLimits is not { } limits)
			{
				_loadError = "no trading metadata for this symbol -- it needs a cTrader session to be fetched once";
				return;
			}
			if (_to <= _from)
			{
				_loadError = "To must be after From";
				return;
			}
			if (_startingCash <= 0)
			{
				_loadError = "starting cash must be positive";
				return;
			}
			if (!ValidateTraderAccountParams())
				return;
			if (_traderCount < 1)
				_traderCount = 1;

			var config = new SimulationConfig(
				_table,
				_from,
				_to,
				_aggregate,
				TimeSpan.FromSeconds(_barSizeSeconds),
				_startingCash,
				_traderCount,
				limits,
				_leverage,
				_stopOutLevelPercent,
				_commissionRatePercent,
				limits.PipSize,
				SelectedRiskPolicies,
				RuleEngineWarmup()
			);

			_keepChartView = false;
			_setupCollapsed = true;
			_playOnLoad = playWhenLoaded;
			if (!SimulationService.TryLoad(config))
			{
				_playOnLoad = false;
				_loadError = "a load is already in progress";
			}
		}

		private bool ValidateTraderAccountParams()
		{
			if (_leverage < 1)
			{
				_loadError = "leverage must be at least 1";
				return false;
			}
			if (_commissionRatePercent < 0)
			{
				_loadError = "commission rate can't be negative";
				return false;
			}
			if (_stopOutLevelPercent <= 0 || _stopOutLevelPercent > 100)
			{
				_loadError = "stop-out level must be between 0 and 100%";
				return false;
			}
			if (_pipSize <= 0)
			{
				_loadError = "pip size must be positive";
				return false;
			}
			return true;
		}

		private void SetRobotPositionOptions(TradeConsoleOptions options) => _robotPositionOptions = options;

		// With a simulation running the new account options go to the bots; with none loaded there is
		// nothing to apply them to, so the load itself picks them up.
		private async Task ApplyAccountSettings()
		{
			_loadError = null;
			if (_startingCash <= 0)
			{
				_loadError = "starting cash must be positive";
				return;
			}
			if (!ValidateTraderAccountParams())
				return;
			if (_traderCount < 1)
				_traderCount = 1;

			if (_snapshot is null)
			{
				await LoadSimulationAsync(playWhenLoaded: false);
				return;
			}
			if (_symbolLimits is not { } limits)
			{
				_loadError = "no trading metadata for this symbol -- it needs a cTrader session to be fetched once";
				return;
			}
			SimulationService.ApplyAccountOptions(
				new BotAccountOptions(_startingCash, _traderCount, _leverage, _commissionRatePercent, _stopOutLevelPercent, limits.PipSize, limits)
			);
		}

		private Task ApplyDataRange() => LoadSimulationAsync(playWhenLoaded: false);

		// Enough history before From that the rule engine's bar window and TxFold lookback are already full when the run starts.
		private TimeSpan RuleEngineWarmup()
		{
			if (!_aggregate)
				return TimeSpan.Zero;

			var barSpan = TimeSpan.FromSeconds(_barSizeSeconds) * (ClampedBarWindow + ClampedBarOffset);
			var txFoldSpan = _txFoldSelectedSeconds.Count > 0 ? TimeSpan.FromSeconds(_txFoldSelectedSeconds.Max()) : TimeSpan.Zero;
			return barSpan > txFoldSpan ? barSpan : txFoldSpan;
		}

		private void SetRobotSubTab(RobotSubTab tab) => _robotSubTab = tab;

		private void SetRobotOpenSide(RobotOpenSide side) => _robotOpenSide = side;

		private void SetRobotEvaluationSeconds(double seconds) => _robotEvaluationSeconds = seconds;

		private void SetRobotEnabled(bool enabled) => _robotEnabled = enabled;

		private void SetTradeConsoleOptions(TradeConsoleOptions options) => _tradeConsoleOptions = options;

		private Task OnRobotRulesChanged() => PersistSettingsIfChangedAsync();

		private RuleEngineResult? ComputeRobotRuleEngine(RobotRuleSet rules) =>
			_snapshot is null ? null : ComputeRuleEngine(_snapshot, rules.XGridPercent, rules.DisabledGroups);

		private IReadOnlyList<string>? SelectedRiskPolicies =>
			_selectedRiskPolicies.Count > 0 ? _selectedRiskPolicies.ToList() : null;

		private void OnAggregationSettingChanged() =>
			SimulationService.SetAggregation(_aggregate, TimeSpan.FromSeconds(_barSizeSeconds));

		// ── Playback actions ──

		private async Task TogglePlayPause()
		{
			if (_snapshot is null)
				await LoadSimulationAsync(playWhenLoaded: true);
			else if (_snapshot.IsPlaying)
				SimulationService.Pause();
			else
				SimulationService.Play();
		}

		private void StepOnce() => SimulationService.Step();

		private void SetSpeed(double multiplier) => SimulationService.SetSpeed(multiplier);

		private static readonly double[] Speeds = [0.1, 1.0, 10.0, 100.0, 1000.0, 10000.0];

		private static int SpeedIndex(SimulationSnapshot s)
		{
			var i = Array.FindIndex(Speeds, sp => Math.Abs(sp - s.SpeedMultiplier) < 0.001);
			return i >= 0 ? i : Array.IndexOf(Speeds, 1.0);
		}

		private void DecreaseSpeed(SimulationSnapshot s) =>
			SimulationService.SetSpeed(Speeds[Math.Max(0, SpeedIndex(s) - 1)]);

		private void IncreaseSpeed(SimulationSnapshot s) =>
			SimulationService.SetSpeed(Speeds[Math.Min(Speeds.Length - 1, SpeedIndex(s) + 1)]);

		private static string FormatSpeed(double v) =>
			(v < 1 ? v.ToString("0.#", CultureInfo.InvariantCulture) : v.ToString("0", CultureInfo.InvariantCulture))
			+ "x";

		private void SetMax() => SimulationService.SetMaxSpeed();

		private void ToggleHideChart() => _hideChart = !_hideChart;

		private void ToggleSetupPanel() => _setupCollapsed = !_setupCollapsed;

		private Task ResetSim()
		{
			_keepChartView = false;
			return LoadSimulationAsync(playWhenLoaded: false);
		}

		// ── Rendering helpers ──

		private static List<CTraderBarRow> ToBarRows(IReadOnlyList<HlocAvdtBar> bars) =>
			bars.Select(b => new CTraderBarRow(
					b.OpenTime,
					b.Open,
					b.High,
					b.Low,
					b.Close,
					b.Volume,
					b.UpVolume,
					b.DownVolume,
					b.Distance
				))
				.ToList();

		private static List<double> ToMidPrices(IReadOnlyList<Tick> ticks) =>
			ticks.Select(t => (t.Bid + t.Ask) / 2.0).ToList();

		private static List<TimedPrice> ToTimedMidPrices(IReadOnlyList<Tick> ticks) =>
			ticks.Select(t => new TimedPrice(t.Timestamp, (t.Bid + t.Ask) / 2.0)).ToList();

		private sealed record TickRow(string Time, string Bid, string Ask, string Mid);

		private List<object> TickRows =>
			_snapshot is null
				? new List<object>()
				: _snapshot
					.RecentTicks.Select(
						object (t) =>
							new TickRow(
								t.Timestamp.ToString("HH:mm:ss.fff"),
								t.Bid.ToString("F5"),
								t.Ask.ToString("F5"),
								((t.Bid + t.Ask) / 2).ToString("F5")
							)
					)
					.Reverse()
					.ToList();

		private sealed record DecisionLogRow(
			string Time,
			string Action,
			string Direction,
			string Size,
			string Price,
			double? RealizedPnl,
			double Commission,
			string Reason
		);

		private static string FormatLogAction(string action) =>
			action switch
			{
				"EnterLong" => "BUY",
				"EnterShort" => "SHORT",
				"Close" => "CLOSE",
				_ => action,
			};

		private List<DecisionLogRow> LogRows =>
			SelectedTrader is not { } trader
				? new List<DecisionLogRow>()
				: trader
					.RecentLog.Select(l => new DecisionLogRow(
						l.TimestampUtc.ToString("HH:mm:ss"),
						FormatLogAction(l.Action),
						l.Direction.ToString(),
						l.Size.ToString("F4"),
						l.Price.ToString("F5"),
						l.RealizedPnl,
						l.Commission,
						l.Reason ?? ""
					))
					.Reverse()
					.ToList();

		private double? RealizedPnlPercentOfBalance() =>
			RiskHelper.PercentOfBalance(SelectedTrader?.Capital.RealizedPnl, SelectedTrader?.Capital.Cash);

		private TradeStatistics? Stats =>
			SelectedTrader is not { } trader
				? null
				: TradeStatistics.From(trader.Trades, trader.MaxEquity, trader.MaxDrawdown);

		private IReadOnlyList<StatGroup> StatGroups => Stats?.Groups ?? [];

		private IReadOnlyList<PositionRowViewModel<Position>> PositionRows
		{
			get
			{
				if (_snapshot is not { } snapshot || SelectedTrader is not { } trader || trader.Position is not { } position)
					return Array.Empty<PositionRowViewModel<Position>>();
				return new[] { ToPositionRow(position, snapshot, trader) };
			}
		}

		private static PositionRowViewModel<Position> ToPositionRow(Position position, SimulationSnapshot snapshot, TraderSnapshot trader)
		{
			bool isLong = position.Direction == PositionDirection.Long;
			double pipSize = snapshot.Config.PipSize;
			double? currentPrice = (isLong ? snapshot.CurrentBid : snapshot.CurrentAsk) ?? snapshot.CurrentPrice;
			double? pips =
				currentPrice is { } price && pipSize > 0
					? (isLong ? price - position.EntryPrice : position.EntryPrice - price) / pipSize
					: null;
			double? pnl =
				snapshot.CurrentBid is { } bid && snapshot.CurrentAsk is { } ask ? position.UnrealizedPnl(bid, ask) : null;
			double? projectedProfit =
				position.TakeProfitPrice is { } tp
					? RiskHelper.RewardOnTarget(position.Direction, position.Size, position.EntryPrice, tp)
					: null;
			double? projectedLoss =
				position.StopLossPrice is { } sl
					? RiskHelper.RiskOnStop(position.Direction, position.Size, position.EntryPrice, sl)
					: null;
			double? rewardToRisk =
				projectedLoss is { } risk && projectedProfit is { } reward ? RiskHelper.RewardToRiskRatio(risk, reward) : null;
			double? percentToTakeProfit = ProgressTowardTarget(position.EntryPrice, position.TakeProfitPrice, pips, pipSize, favorable: true);
			double? percentToStopLoss = ProgressTowardTarget(position.EntryPrice, position.StopLossPrice, pips, pipSize, favorable: false);
			double? percentNet = RiskHelper.PercentOfBalance(pnl, trader.Capital.Cash);
			var duration = (snapshot.CurrentTimestampUtc ?? DateTime.UtcNow) - position.EntryTime;

			return new PositionRowViewModel<Position>(
				position,
				snapshot.Config.Table,
				isLong,
				isLong ? "LONG" : "SHORT",
				position.Size.ToString("N2"),
				position.EntryPrice.ToString("N5"),
				pips,
				PositionRowFormat.PipsLabel(pips),
				pnl,
				PositionRowFormat.PnlLabel(pnl),
				position.EntryTime.ToString("HH:mm:ss"),
				position.StopLossPrice?.ToString("N5") ?? "—",
				position.TakeProfitPrice?.ToString("N5") ?? "—",
				projectedProfit,
				PositionRowFormat.MoneyLabel(projectedProfit),
				projectedLoss,
				PositionRowFormat.MoneyLabel(-projectedLoss),
				percentToTakeProfit,
				PositionRowFormat.PercentLabel(percentToTakeProfit),
				percentToStopLoss,
				PositionRowFormat.PercentLabel(percentToStopLoss),
				rewardToRisk,
				PositionRowFormat.RewardToRiskLabel(rewardToRisk),
				percentNet,
				PositionRowFormat.PercentLabel(percentNet),
				PositionRowFormat.DurationLabel(duration)
			);
		}

		/// <summary>Progress from entry toward a target price, as a percent of the entry-to-target pip distance.</summary>
		private static double? ProgressTowardTarget(double entryPrice, double? targetPrice, double? movedPips, double pipSize, bool favorable)
		{
			if (targetPrice is not { } target || pipSize <= 0 || movedPips is not { } moved)
				return null;
			double totalPips = Math.Abs(target - entryPrice) / pipSize;
			return totalPips <= 0 ? null : (favorable ? moved : -moved) / totalPips * 100;
		}
	}
}
