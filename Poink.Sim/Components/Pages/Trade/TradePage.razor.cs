using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Components.Shared.FinancialsPanel;
using Poink.Sim.Components.Shared.PositionTable;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData.Recording;
using Poink.Sim.Services.Symbols;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Lays out the Trade page's four panels plus the account menu and connects each to its own narrow driver interface. Cross-panel state (selected symbol, side-panel collapse, settings persistence) lives in the injected TradeCoordinator, not here.</summary>
	public partial class TradePage : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private ITradeCoordinator Coordinator { get; set; } = null!;

		[Inject]
		private ITickStreamAccess Session { get; set; } = null!;

		[Inject]
		private ITradeExecutionAccess TradeExecution { get; set; } = null!;

		[Inject]
		private IBreakevenCloseService BreakevenClose { get; set; } = null!;

		[Inject]
		private ISymbolMetadataCache MetadataCache { get; set; } = null!;

		[Inject]
		private IJSRuntime JS { get; set; } = null!;

		[Inject]
		private ITradeDraftService TradeDraft { get; set; } = null!;

		[Inject]
		private ITradeHistoryService History { get; set; } = null!;

		[Inject]
		private ILiveTickRecorder TickRecorder { get; set; } = null!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		[Inject]
		private ISessionStorageHelper SessionStorage { get; set; } = null!;

		[Inject]
		private IAccountAccess Account { get; set; } = null!;

		private const string ConsoleModeStorageKey = "trade.consoleMode";

		private const string BreakevenStorageKey = "trade.breakevenArmed";

		/// <summary>cTrader expresses symbol volume limits in hundredths of a unit.</summary>
		private const double CTraderVolumeScale = 100.0;

		private static readonly TimeSpan HistoryLookback = TimeSpan.FromDays(30);

		/// <summary>How long a quick-trade side stays locked waiting for cTrader to report a fill or close.</summary>
		private static readonly TimeSpan QuickTradeReportTimeout = TimeSpan.FromSeconds(10);

		private enum RealizedWindow
		{
			Hour,
			Day,
			Week,
			Month,
		}

		private IAsyncDisposable? _tickRecordingLease;
		private TradeChartPanel? _chartPanelRef;
		private TradeConsole? _consoleRef;
		private bool _consoleBusy;
		private string? _consoleStatus;
		private bool _consoleStatusIsError;
		private TradeConsoleMode _consoleMode = TradeConsoleMode.Simple;
		private bool _quickLongBusy;
		private bool _quickShortBusy;
		private readonly Dictionary<bool, QuickTradeAwaitedReport> _quickAwaitedReports = new();
		private readonly CancellationTokenSource _disposed = new();
		private bool _breakevenRestored;
		private XGridResult? _xgrid;

		private RealizedWindow _realizedWindow = RealizedWindow.Day;
		private double? _realizedPnl;
		private bool _realizedLoading;
		private bool _realizedPickerOpen;
		private HashSet<long> _knownPositionIds = new();

		private IReadOnlyList<CTraderDealInfo> _closedDeals = Array.Empty<CTraderDealInfo>();
		private bool _historyLoading;
		private string? _historyError;

		private static TimeSpan ToTimeSpan(RealizedWindow window) =>
			window switch
			{
				RealizedWindow.Hour => TimeSpan.FromHours(1),
				RealizedWindow.Week => TimeSpan.FromDays(7),
				RealizedWindow.Month => TimeSpan.FromDays(30),
				_ => TimeSpan.FromDays(1),
			};

		private static string RealizedWindowLabel(RealizedWindow window) =>
			window switch
			{
				RealizedWindow.Hour => "1H",
				RealizedWindow.Week => "1W",
				RealizedWindow.Month => "1M",
				_ => "1D",
			};

		private void ToggleRealizedPicker() => _realizedPickerOpen = !_realizedPickerOpen;

		private async Task SelectRealizedWindow(RealizedWindow window)
		{
			_realizedWindow = window;
			_realizedPickerOpen = false;
			await LoadRealizedPnlAsync();
		}

		private async Task LoadRealizedPnlAsync()
		{
			_realizedLoading = true;
			StateHasChanged();
			try
			{
				var toUtc = DateTime.UtcNow;
				var deals = await History.GetClosedDealsAsync(toUtc - ToTimeSpan(_realizedWindow), toUtc);
				_realizedPnl = deals.Sum(d => (d.RealizedPnl ?? 0) + (d.Commission ?? 0) + (d.Swap ?? 0));
			}
			finally
			{
				_realizedLoading = false;
			}
		}

		private async Task LoadHistoryAsync()
		{
			_historyLoading = true;
			_historyError = null;
			StateHasChanged();
			try
			{
				var toUtc = DateTime.UtcNow;
				_closedDeals = await History.GetClosedDealsAsync(toUtc - HistoryLookback, toUtc);
			}
			catch (Exception ex)
			{
				_closedDeals = Array.Empty<CTraderDealInfo>();
				_historyError = ex.Message;
			}
			finally
			{
				_historyLoading = false;
			}
		}

		private void HandleFinancialsTabSelected(FinancialsPanel.FinancialsTab tab)
		{
			if (tab is FinancialsPanel.FinancialsTab.History or FinancialsPanel.FinancialsTab.Statistics)
				_ = LoadHistoryAsync();
		}

		private IReadOnlyList<StatGroup> StatGroups => LiveTradeStatisticsHelper.From(_closedDeals);

		// Every open position's symbol is auto-subscribed independently of the watchlist, so this excludes
		// a position only in the brief window before its subscription/metadata/first quote lands.
		private double? UnrealizedPnl
		{
			get
			{
				var known = Session
					.Positions.Select(p => PositionPnlHelper.UnrealizedPnl(p, ChartQuoteFor(p.SymbolId), MetadataCache.Get(p.SymbolId)))
					.Where(pnl => pnl is not null)
					.Select(pnl => pnl!.Value)
					.ToList();
				return known.Count == 0 ? null : known.Sum();
			}
		}

		private CTraderSymbolQuote? ChartQuoteFor(long symbolId) => Session.Quotes.FirstOrDefault(q => q.SymbolId == symbolId);

		private double? RealizedPnlPercentOfBalance => RiskHelper.PercentOfBalance(_realizedPnl, Session.Balance);

		private double? Equity => Session.Balance is { } balance && UnrealizedPnl is { } pnl ? balance + pnl : null;

		private double? MarginUsed => Session.Positions.Count == 0 ? null : Session.Positions.Sum(p => p.UsedMargin);

		private double? MarginLevel =>
			Equity is { } equity && MarginUsed is { } used && used != 0 ? equity / used * 100 : null;

		private double? FreeMargin => Equity is { } equity && MarginUsed is { } used ? equity - used : null;

		private double? AvailableMargin => Session.Positions.Count == 0 ? Session.Balance : FreeMargin;

		private IReadOnlyList<PositionRowViewModel<CTraderPositionInfo>> PositionRows => Session.Positions.Select(ToPositionRow).ToList();

		private PositionRowViewModel<CTraderPositionInfo> ToPositionRow(CTraderPositionInfo position)
		{
			var quote = ChartQuoteFor(position.SymbolId);
			var metadata = MetadataCache.Get(position.SymbolId);
			var pips = PositionPnlHelper.PipDifference(position, quote, metadata);
			var pnl = PositionPnlHelper.UnrealizedPnl(position, quote, metadata);
			var projectedProfit = PositionRiskHelper.ProjectedProfit(position, metadata);
			var projectedLoss = PositionRiskHelper.ProjectedLoss(position, metadata);
			var percentToTakeProfit = PositionRiskHelper.PercentToTakeProfit(position, quote, metadata);
			var percentToStopLoss = PositionRiskHelper.PercentToStopLoss(position, quote, metadata);
			var rewardToRisk = PositionRiskHelper.RewardToRisk(position, metadata);
			var percentNet = RiskHelper.PercentOfBalance(pnl, Session.Balance);
			var duration = DateTime.UtcNow - position.OpenTimeUtc;

			return new PositionRowViewModel<CTraderPositionInfo>(
				position,
				position.SymbolName,
				position.IsLong,
				position.IsLong ? "LONG" : "SHORT",
				position.Volume.ToString("N2"),
				position.EntryPrice.ToString("N5"),
				pips,
				PositionRowFormat.PipsLabel(pips),
				pnl,
				PositionRowFormat.PnlLabel(pnl),
				position.OpenTimeUtc.ToLocalTime().ToString("HH:mm:ss"),
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

		private CTraderSymbolQuote? ChartQuote =>
			Coordinator.ChartSymbolId is { } id ? Session.Quotes.FirstOrDefault(q => q.SymbolId == id) : null;

		// TradeConsole's SL/TP pip math needs the symbol's real pip size; 0.0001 is only a
		// fallback for the moment before EnsureAsync (triggered from watch/position-load) resolves it.
		private double ChartPipSize =>
			Coordinator.ChartSymbolId is { } id && MetadataCache.Get(id) is { } metadata ? PositionPnlHelper.PipSize(metadata) : 0.0001;

		private SymbolMetadataModel? Metadata => Coordinator.ChartSymbolId is { } id ? MetadataCache.Get(id) : null;

		/// <summary>
		/// The symbol's minimum SL/TP distance from entry, in price terms. A %balance-derived price
		/// closer than this would be rejected by cTrader, so it gets pushed out to the minimum.
		/// </summary>
		private double? MinStopLossDistancePrice =>
			Metadata is { } metadata && PositionPnlHelper.MinDistancePips(metadata, metadata.SlDistance) is { } pips
				? pips * ChartPipSize
				: null;

		private double? MinTakeProfitDistancePrice =>
			Metadata is { } metadata && PositionPnlHelper.MinDistancePips(metadata, metadata.TpDistance) is { } pips
				? pips * ChartPipSize
				: null;

		/// <summary>
		/// The symbol's smallest price increment (from its Digits), e.g. 0.01 for a 2-digit symbol.
		/// SL/TP prices are rounded to this so a %balance-derived price -- or plain float noise from
		/// pip arithmetic -- never carries more decimals than cTrader accepts for the symbol.
		/// </summary>
		private double? PriceStep => Metadata is { } metadata ? 1.0 / Math.Pow(10, metadata.Digits) : null;

		private double? ChartMinVolume => Metadata is { MinVolume: > 0 } metadata ? metadata.MinVolume / CTraderVolumeScale : null;

		private double? ChartVolumeStep => Metadata is { StepVolume: > 0 } metadata ? metadata.StepVolume / CTraderVolumeScale : null;

		private ElementReference _positionsHDividerRef;
		private IJSObjectReference? _dividerResizeInstance;

		// Drag-to-resize for the divider between the chart region and the financials panel --
		// the chart row's own divider/resize script lives in TradeChartPanel now.
		private const string PositionsDividerScript = """
			(function() {
			    function bindHeightAfter(divider) {
			        if (!divider || !divider.dataset || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minHeight = parseInt(divider.dataset.minHeight, 10) || 120;
			        var maxHeightVh = parseInt(divider.dataset.maxHeightVh, 10) || 70;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var target = divider.nextElementSibling;
			            if (!target) return;
			            var startY = e.clientY;
			            var startHeight = target.getBoundingClientRect().height;
			            var maxHeight = (maxHeightVh / 100) * window.innerHeight;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('trade-chart-hdivider-active');

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
			                divider.classList.remove('trade-chart-hdivider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    return { bindHeightAfter: bindHeightAfter };
			})()
			""";

		protected override void OnInitialized()
		{
			Coordinator.Changed += HandleCoordinatorChanged;
			BreakevenClose.Changed += HandleBreakevenChanged;
			BreakevenClose.CloseReported += HandleBreakevenCloseReported;
			_tickRecordingLease = TickRecorder.Acquire();
			_knownPositionIds = Session.Positions.Select(p => p.PositionId).ToHashSet();
			_ = LoadRealizedPnlAsync();
		}

		// A position vanishing from the live list (rather than appearing) means it closed, so Realized
		// -- otherwise only refreshed on init or window change -- would go stale without this check.
		private void HandleCoordinatorChanged()
		{
			var currentPositionIds = Session.Positions.Select(p => p.PositionId).ToHashSet();
			if (_knownPositionIds.Any(id => !currentPositionIds.Contains(id)))
				_ = LoadRealizedPnlAsync();
			_knownPositionIds = currentPositionIds;
			InvokeAsync(StateHasChanged);
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", PositionsDividerScript);
				await Coordinator.LoadPersistedSettingsAsync();
				_consoleMode = await LocalStorage.GetItemAsync<TradeConsoleMode?>(ConsoleModeStorageKey) ?? TradeConsoleMode.Simple;
				StateHasChanged();
			}
			else
			{
				await Coordinator.PersistSettingsIfChangedAsync(_chartPanelRef?.CurrentSettings);
			}

			if (!_breakevenRestored)
				await RestoreBreakevenAsync();

			if (_dividerResizeInstance is not null && Coordinator.IsConnected)
				await _dividerResizeInstance.InvokeVoidAsync("bindHeightAfter", _positionsHDividerRef);
		}

		public async ValueTask DisposeAsync()
		{
			Coordinator.Changed -= HandleCoordinatorChanged;
			BreakevenClose.Changed -= HandleBreakevenChanged;
			BreakevenClose.CloseReported -= HandleBreakevenCloseReported;
			TradeDraft.Clear();
			_disposed.Cancel();

			if (_tickRecordingLease is not null)
				await _tickRecordingLease.DisposeAsync();

			if (_dividerResizeInstance is not null)
			{
				try
				{
					await _dividerResizeInstance.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
			}
		}

		private async Task ClosePositionAsync(CTraderPositionInfo position)
		{
			ShowConsoleResult(await TradeExecution.ClosePositionAsync(position.PositionId));
		}

		private async Task HandleConsoleSubmit(TradeConsoleOrderRequest request)
		{
			_consoleStatus = null;
			if (Coordinator.ChartSymbolId is not { } symbolId)
				return;

			_consoleBusy = true;
			StateHasChanged();
			try
			{
				var result = await TradeExecution.PlaceOrderAsync(
					symbolId,
					request.IsLong,
					request.Volume,
					request.OrderType switch
					{
						OrderType.Limit => CTraderOrderType.Limit,
						OrderType.Stop => CTraderOrderType.Stop,
						_ => CTraderOrderType.Market,
					},
					request.TriggerPrice,
					request.StopLossPrice,
					request.TakeProfitPrice
				);
				_consoleStatus = result.Message;
				_consoleStatusIsError = !result.Success;
				if (result.Success)
					_consoleRef?.ClearDraftPreview();
			}
			finally
			{
				_consoleBusy = false;
			}
		}

		private async Task SetConsoleModeAsync(TradeConsoleMode mode)
		{
			if (mode == _consoleMode)
				return;

			_consoleMode = mode;
			_consoleStatus = null;
			if (mode == TradeConsoleMode.Simple)
				TradeDraft.Clear();
			await LocalStorage.SetItemAsync(ConsoleModeStorageKey, mode);
		}

		private void HandleXGridChanged(XGridResult? grid) => _xgrid = grid;

		private QuickTradeSideState QuickSide(bool isLong)
		{
			var positions = Coordinator.ChartSymbolId is { } symbolId
				? QuickTradeHelper.SidePositions(Session.Positions, symbolId, isLong)
				: Array.Empty<CTraderPositionInfo>();
			var inFlight = isLong ? _quickLongBusy : _quickShortBusy;
			return new QuickTradeSideState(
				isLong,
				positions.Count,
				positions.Sum(p => p.Volume),
				inFlight || IsAwaitingReport(isLong, positions.Count),
				Coordinator.ChartSymbolId is { } id && BreakevenClose.IsArmed(new BreakevenSide(id, isLong))
			);
		}

		private bool IsAwaitingReport(bool isLong, int currentCount) =>
			_quickAwaitedReports.TryGetValue(isLong, out var awaited)
			&& awaited.SymbolId == Coordinator.ChartSymbolId
			&& QuickTradeHelper.IsAwaitingReport(awaited, currentCount, DateTime.UtcNow);

		private async Task HandleQuickOpenAsync(QuickTradeOpenRequest request)
		{
			if (Coordinator.ChartSymbolId is not { } symbolId)
				return;
			var baseline = QuickTradeHelper.SidePositions(Session.Positions, symbolId, request.IsLong).Count;

			SetQuickBusy(request.IsLong, true);
			try
			{
				var result = await TradeExecution.PlaceOrderAsync(
					symbolId,
					request.IsLong,
					request.Volume,
					CTraderOrderType.Market,
					null,
					null,
					null
				);
				ShowConsoleResult(result);
				if (result.Success)
					AwaitReport(request.IsLong, new QuickTradeAwaitedReport(symbolId, true, baseline, ReportDeadline()));
			}
			finally
			{
				SetQuickBusy(request.IsLong, false);
			}
		}

		private async Task HandleQuickCloseAsync(bool isLong)
		{
			if (Coordinator.ChartSymbolId is not { } symbolId)
				return;
			var positions = QuickTradeHelper.SidePositions(Session.Positions, symbolId, isLong);
			if (positions.Count == 0)
				return;

			SetQuickBusy(isLong, true);
			try
			{
				var results = await Task.WhenAll(positions.Select(p => TradeExecution.ClosePositionAsync(p.PositionId)));
				var summary = QuickTradeHelper.SummarizeClose(isLong, results);
				ShowConsoleResult(summary);
				if (summary.Success)
					AwaitReport(isLong, new QuickTradeAwaitedReport(symbolId, false, positions.Count, ReportDeadline()));
			}
			finally
			{
				SetQuickBusy(isLong, false);
			}
		}

		private static DateTime ReportDeadline() => DateTime.UtcNow + QuickTradeReportTimeout;

		private void AwaitReport(bool isLong, QuickTradeAwaitedReport awaited)
		{
			_quickAwaitedReports[isLong] = awaited;
			_ = ReleaseUnreportedAsync(isLong, awaited);
		}

		// The lock lifts as soon as cTrader's position event re-renders the page; this only covers a report that never comes.
		private async Task ReleaseUnreportedAsync(bool isLong, QuickTradeAwaitedReport awaited)
		{
			try
			{
				await Task.Delay(QuickTradeReportTimeout, _disposed.Token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			if (!_quickAwaitedReports.TryGetValue(isLong, out var current) || current != awaited)
				return;

			_quickAwaitedReports.Remove(isLong);
			var count = QuickTradeHelper.SidePositions(Session.Positions, awaited.SymbolId, isLong).Count;
			if (!QuickTradeHelper.IsReported(awaited, count))
				ShowConsoleResult(
					new CTraderOrderResult(
						false,
						$"cTrader has not reported the {(isLong ? "long" : "short")} {(awaited.IsOpening ? "open" : "close")} after {QuickTradeReportTimeout.TotalSeconds:0}s; check Positions before trading again."
					)
				);
			await InvokeAsync(StateHasChanged);
		}

		private void SetQuickBusy(bool isLong, bool busy)
		{
			if (isLong)
				_quickLongBusy = busy;
			else
				_quickShortBusy = busy;
		}

		private void HandleQuickBreakevenToggle(bool isLong)
		{
			if (Coordinator.ChartSymbolId is not { } symbolId)
				return;

			var side = new BreakevenSide(symbolId, isLong);
			if (BreakevenClose.IsArmed(side))
				BreakevenClose.Disarm(side);
			else
				BreakevenClose.Arm(side);
		}

		private void HandleBreakevenChanged() =>
			InvokeAsync(async () =>
			{
				await SaveBreakevenAsync();
				StateHasChanged();
			});

		// Waits for an account so stored sides are matched to it, and never overwrites the tab's copy before reading it.
		private async Task RestoreBreakevenAsync()
		{
			if (Account.SelectedAccountId is not { } accountId)
				return;

			var entries = await SessionStorage.GetItemAsync<List<BreakevenArmedEntry>>(BreakevenStorageKey);
			foreach (var side in BreakevenCloseHelper.SidesToRearm(entries, accountId))
				BreakevenClose.Arm(side);

			_breakevenRestored = true;
			await SaveBreakevenAsync();
		}

		private async Task SaveBreakevenAsync()
		{
			if (!_breakevenRestored)
				return;

			var entries = Account.SelectedAccountId is { } accountId
				? BreakevenCloseHelper.ToEntries(accountId, BreakevenClose.ArmedSides)
				: Array.Empty<BreakevenArmedEntry>();
			await SessionStorage.SetItemAsync(BreakevenStorageKey, entries);
		}

		private void HandleBreakevenCloseReported(CTraderOrderResult result) =>
			InvokeAsync(() =>
			{
				ShowConsoleResult(result);
				StateHasChanged();
			});

		private void ShowConsoleResult(CTraderOrderResult result)
		{
			_consoleStatus = result.Message;
			_consoleStatusIsError = !result.Success;
		}

		private void HandleDraftChanged(TradeConsoleDraft? draft)
		{
			if (Coordinator.ChartSymbolId is not { } symbolId || draft is not { } d)
			{
				TradeDraft.Clear();
				return;
			}
			TradeDraft.Update(new TradeDraft(symbolId, d.IsLong, d.EntryPrice, d.Volume, d.StopLossPrice, d.TakeProfitPrice));
		}
	}
}
