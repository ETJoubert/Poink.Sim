using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Connects the Trade page's four regions -- SymbolWatchlistPanel (left), TradeChartPanel (charts), FinancialsPanel (positions/financials) and TradeControlsPanel (right) -- plus the header/account chrome. Owns only cross-component selection state and settings persistence; each region's own display logic and calculations live in that component.</summary>
	public partial class TradePage : ComponentBase, IDisposable, IAsyncDisposable
	{
		[Inject]
		private ICTraderSessionService Session { get; set; } = null!;

		[Inject]
		private ISymbolMetadataCache MetadataCache { get; set; } = null!;

		[Inject]
		private NavigationManager Navigation { get; set; } = null!;

		[Inject]
		private IJSRuntime JS { get; set; } = null!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		private int _barSizeSeconds = 60;
		private bool _sidepanelCollapsed;
		private bool _rightPanelCollapsed;

		private long? _chartSymbolId;
		private string? _chartSymbolName;
		private TradeChartPanel? _chartPanelRef;
		private ChartPanelSettings? _pendingChartSettings;

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private bool _accountMenuOpen;

		private CTraderAccountInfo? SelectedAccount =>
			Session.Accounts.FirstOrDefault(a => a.AccountId == Session.SelectedAccountId);

		private void ToggleAccountMenu() => _accountMenuOpen = !_accountMenuOpen;

		private void CloseAccountMenu() => _accountMenuOpen = false;

		private void ToggleRightPanel() => _rightPanelCollapsed = !_rightPanelCollapsed;

		private CTraderSymbolQuote? ChartQuote =>
			_chartSymbolId is { } id ? Session.Quotes.FirstOrDefault(q => q.SymbolId == id) : null;

		// TradeControlsPanel's SL/TP pip math needs the symbol's real pip size; 0.0001 is only a
		// fallback for the moment before EnsureAsync (triggered from watch/position-load) resolves it.
		private double ChartPipSize =>
			_chartSymbolId is { } id && MetadataCache.Get(id) is { } metadata ? PositionPnlHelper.PipSize(metadata) : 0.0001;

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
		}

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

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", PositionsDividerScript);
				await LoadPersistedSettingsAsync();

				// Session and its Positions are a long-lived singleton, so navigating back to an already-
				// connected session raises no Changed event to trigger this -- check once on mount too.
				if (IsConnected)
					await MetadataCache.EnsureForPositionsAsync(Session.Positions);
			}
			else
			{
				await PersistSettingsIfChangedAsync();
			}

			if (_dividerResizeInstance is not null && IsConnected)
				await _dividerResizeInstance.InvokeVoidAsync("bindHeightAfter", _positionsHDividerRef);
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

		// Round-trips layout/chart settings and the watchlist through localStorage, since this page's,
		// TradeChartPanel's and CTraderSessionService's in-memory state don't survive navigation/restart.
		private const string SettingsStorageKey = "poink.trade.setup";

		private string? _lastPersistedSettingsJson;
		private bool _restorePending;
		private List<long> _pendingWatchSymbolIds = new();
		private long? _pendingChartSymbolId;
		private string? _pendingChartSymbolName;

		private TradePageSettings CurrentSettings
		{
			get
			{
				var chart = _chartPanelRef?.CurrentSettings;
				return new(
					_barSizeSeconds,
					chart?.ShowMainChart ?? true,
					chart?.ShowXGrid ?? false,
					chart?.ShowVolumeProfile ?? false,
					chart?.ShowTimeProfile ?? false,
					chart?.ShowProfileChartLines ?? true,
					chart?.ShowVwap ?? true,
					chart?.BarWindowSize ?? 50,
					chart?.BarWindowOffset ?? 0,
					chart?.XGridPipCount ?? 20,
					chart?.XGridColCount ?? 20,
					_chartSymbolId,
					_chartSymbolName,
					Session.Watchlist.Select(q => q.SymbolId).ToList(),
					_sidepanelCollapsed,
					_rightPanelCollapsed
				);
			}
		}

		private async Task LoadPersistedSettingsAsync()
		{
			var settings = await LocalStorage.GetItemAsync<TradePageSettings>(SettingsStorageKey);
			if (settings is null)
				return;

			_barSizeSeconds = settings.BarSizeSeconds;
			_pendingChartSettings = new ChartPanelSettings(
				settings.ShowMainChart,
				settings.ShowXGrid,
				settings.ShowVolumeProfile,
				settings.ShowTimeProfile,
				settings.ShowProfileChartLines,
				settings.ShowVwap,
				settings.BarWindowSize,
				settings.BarWindowOffset,
				settings.XGridPipCount,
				settings.XGridColCount
			);
			_sidepanelCollapsed = settings.SidepanelCollapsed;
			_rightPanelCollapsed = settings.RightPanelCollapsed;
			_pendingChartSymbolId = settings.ChartSymbolId;
			_pendingChartSymbolName = settings.ChartSymbolName;
			_pendingWatchSymbolIds = settings.WatchedSymbolIds;
			_restorePending = true;

			if (IsConnected)
				await ApplyPendingRestoreAsync();

			StateHasChanged();
		}

		// Re-subscribes whatever this browser had watched and reopens its last chart -- called once
		// settings load if already connected, or on the next Connected transition otherwise.
		private async Task ApplyPendingRestoreAsync()
		{
			if (!_restorePending)
				return;
			_restorePending = false;

			foreach (var symbolId in _pendingWatchSymbolIds)
			{
				if (Session.IsSymbolWatched(symbolId))
					continue;

				await Session.WatchSymbolAsync(symbolId);
				await MetadataCache.EnsureAsync(symbolId);
			}

			if (_pendingChartSymbolId is { } id && _pendingChartSymbolName is { } name && _chartSymbolId is null)
				OpenChart(id, name);
		}

		private async Task PersistSettingsIfChangedAsync()
		{
			var json = JsonSerializer.Serialize(CurrentSettings);
			if (json == _lastPersistedSettingsJson)
				return;

			_lastPersistedSettingsJson = json;
			await LocalStorage.SetItemAsync(SettingsStorageKey, CurrentSettings);
		}

		private void LogIn()
		{
			if (Session.BuildAuthorizationUri() is not { } authUri)
				return;

			Navigation.NavigateTo(authUri.ToString(), forceLoad: true);
		}

		private async Task LogOutAsync()
		{
			_accountMenuOpen = false;
			await Session.LogoutAsync();
		}

		private async Task ClosePositionAsync(CTraderPositionInfo position)
		{
			if (
				Session.UseLiveEnvironment
				&& !await JS.InvokeAsync<bool>(
					"confirm",
					$"Close the {(position.IsLong ? "LONG" : "SHORT")} position on {position.SymbolName}?"
				)
			)
				return;

			await Session.ClosePositionAsync(position.PositionId);
		}

		private void OpenChart(long symbolId, string symbolName)
		{
			_chartSymbolId = symbolId;
			_chartSymbolName = symbolName;
		}

		private void HandleSymbolSelected((long SymbolId, string SymbolName) selection) =>
			OpenChart(selection.SymbolId, selection.SymbolName);

		// Unwatching the symbol whose chart is open closes that chart too, since a chart
		// with no live quote behind it can no longer receive ticks.
		private void HandleSymbolUnwatched(long symbolId)
		{
			if (_chartSymbolId != symbolId)
				return;

			_chartSymbolId = null;
			_chartSymbolName = null;
		}

		private void OnSessionChanged() =>
			InvokeAsync(async () =>
			{
				if (_restorePending && IsConnected)
					await ApplyPendingRestoreAsync();
				await MetadataCache.EnsureForPositionsAsync(Session.Positions);
				StateHasChanged();
			});

		public void Dispose()
		{
			Session.Changed -= OnSessionChanged;
		}
	}
}
