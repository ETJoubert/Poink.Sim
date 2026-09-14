using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.Symbols;

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
		private ISymbolMetadataCache MetadataCache { get; set; } = null!;

		[Inject]
		private IJSRuntime JS { get; set; } = null!;

		[Inject]
		private ITradeDraftService TradeDraft { get; set; } = null!;

		private TradeChartPanel? _chartPanelRef;
		private TradeConsole? _consoleRef;
		private bool _consoleBusy;
		private string? _consoleStatus;
		private bool _consoleStatusIsError;

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
		}

		// Only the watchlist panel and account menu re-render themselves on session updates, so this
		// page needs its own subscription to keep FinancialsPanel's parameters current.
		private void HandleCoordinatorChanged() => InvokeAsync(StateHasChanged);

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", PositionsDividerScript);
				await Coordinator.LoadPersistedSettingsAsync();
				StateHasChanged();
			}
			else
			{
				await Coordinator.PersistSettingsIfChangedAsync(_chartPanelRef?.CurrentSettings);
			}

			if (_dividerResizeInstance is not null && Coordinator.IsConnected)
				await _dividerResizeInstance.InvokeVoidAsync("bindHeightAfter", _positionsHDividerRef);
		}

		public async ValueTask DisposeAsync()
		{
			Coordinator.Changed -= HandleCoordinatorChanged;
			TradeDraft.Clear();

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
			if (
				TradeExecution.UseLiveEnvironment
				&& !await JS.InvokeAsync<bool>(
					"confirm",
					$"Close the {(position.IsLong ? "LONG" : "SHORT")} position on {position.SymbolName}?"
				)
			)
				return;

			await TradeExecution.ClosePositionAsync(position.PositionId);
		}

		private async Task HandleConsoleSubmit(TradeConsoleOrderRequest request)
		{
			_consoleStatus = null;
			if (Coordinator.ChartSymbolId is not { } symbolId)
				return;

			if (
				TradeExecution.UseLiveEnvironment
				&& !await JS.InvokeAsync<bool>(
					"confirm",
					$"Place a LIVE {(request.IsLong ? "BUY" : "SELL")} order for {Coordinator.ChartSymbolName ?? "this symbol"}?"
				)
			)
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
