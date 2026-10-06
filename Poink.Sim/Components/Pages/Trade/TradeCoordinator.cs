using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Scoped, so it's shared across TradePage and its panels for one circuit without being a
	/// component itself. <see cref="Changed"/> mirrors the same background-thread-notification
	/// pattern <see cref="ICTraderSessionService.Changed"/> already uses -- subscribers marshal onto
	/// their own render context themselves (see TradePage.HandleCoordinatorChanged).
	/// </summary>
	internal sealed class TradeCoordinator : ITradeCoordinator, IDisposable
	{
		private const string SettingsStorageKey = "poink.trade.setup";
		private const string WatchlistStorageKeyPrefix = "poink.trade.watchlist.";

		private readonly IWatchlistAccess _watchlistAccess;
		private readonly ITickStreamAccess _tickStreamAccess;
		private readonly ISymbolMetadataCache _metadataCache;
		private readonly ILocalStorageHelper _localStorage;

		private string? _lastPersistedSettingsJson;
		private bool _restorePending;
		private long? _pendingChartSymbolId;
		private string? _pendingChartSymbolName;

		private bool _watchlistSynced;
		private long? _watchlistSyncedAccountId;
		private string? _lastPersistedWatchlistJson;

		public TradeCoordinator(
			IWatchlistAccess watchlistAccess,
			ITickStreamAccess tickStreamAccess,
			ISymbolMetadataCache metadataCache,
			ILocalStorageHelper localStorage
		)
		{
			_watchlistAccess = watchlistAccess;
			_tickStreamAccess = tickStreamAccess;
			_metadataCache = metadataCache;
			_localStorage = localStorage;
			_watchlistAccess.Changed += OnSessionChanged;
		}

		public event Action? Changed;

		public bool IsConnected => _watchlistAccess.State == CTraderConnectionState.Connected;

		public int BarSizeSeconds { get; set; } = 60;

		public bool SidepanelCollapsed { get; set; }

		public bool RightPanelCollapsed { get; private set; }

		public long? ChartSymbolId { get; private set; }

		public string? ChartSymbolName { get; private set; }

		public ChartPanelSettings? PendingChartSettings { get; private set; }

		public void ToggleRightPanel() => RightPanelCollapsed = !RightPanelCollapsed;

		public void OpenChart(long symbolId, string symbolName)
		{
			ChartSymbolId = symbolId;
			ChartSymbolName = symbolName;
		}

		public void HandleSymbolSelected((long SymbolId, string SymbolName) selection) =>
			OpenChart(selection.SymbolId, selection.SymbolName);

		// Unwatching the charted symbol closes its chart, since a chart with no live quote can no longer receive ticks.
		public void HandleSymbolUnwatched(long symbolId)
		{
			if (ChartSymbolId != symbolId)
				return;

			ChartSymbolId = null;
			ChartSymbolName = null;
		}

		public async Task LoadPersistedSettingsAsync()
		{
			var settings = await _localStorage.GetItemAsync<TradePageSettings>(SettingsStorageKey);
			if (settings is not null)
			{
				BarSizeSeconds = settings.BarSizeSeconds;
				PendingChartSettings = TradeChartSettingsHelper.FromPersisted(settings);
				SidepanelCollapsed = settings.SidepanelCollapsed;
				RightPanelCollapsed = settings.RightPanelCollapsed;
				_pendingChartSymbolId = settings.ChartSymbolId;
				_pendingChartSymbolName = settings.ChartSymbolName;
				_restorePending = true;

				if (IsConnected)
					ApplyPendingRestore();
			}

			// Session and its Positions are a long-lived singleton, so navigating back to an already-
			// connected session raises no Changed event to trigger this -- check once on mount too.
			if (IsConnected)
			{
				await SyncAccountWatchlistAsync();
				await _metadataCache.EnsureForPositionsAsync(_tickStreamAccess.Positions);
			}
		}

		public async Task PersistSettingsIfChangedAsync(ChartPanelSettings? chartSettings)
		{
			var settings = BuildSettings(chartSettings);
			var json = JsonSerializer.Serialize(settings);
			if (json != _lastPersistedSettingsJson)
			{
				_lastPersistedSettingsJson = json;
				await _localStorage.SetItemAsync(SettingsStorageKey, settings);
			}

			await PersistWatchlistIfChangedAsync();
		}

		private TradePageSettings BuildSettings(ChartPanelSettings? chart) =>
			TradeChartSettingsHelper.ToPersisted(
				chart ?? PendingChartSettings,
				BarSizeSeconds,
				ChartSymbolId,
				ChartSymbolName,
				SidepanelCollapsed,
				RightPanelCollapsed
			);

		// Reopens this browser's last chart once settings load while connected, or on the next Connected transition.
		private void ApplyPendingRestore()
		{
			if (!_restorePending)
				return;
			_restorePending = false;

			if (_pendingChartSymbolId is { } id && _pendingChartSymbolName is { } name && ChartSymbolId is null)
				OpenChart(id, name);
		}

		private static string WatchlistStorageKey(long accountId) => $"{WatchlistStorageKeyPrefix}{accountId}";

		private async Task PersistWatchlistIfChangedAsync()
		{
			if (_watchlistAccess.SelectedAccountId is not { } accountId)
				return;

			// Mid-switch, SelectedAccountId flips to the new account before its watchlist is
			// restored -- persisting here would overwrite its storage with a false-empty snapshot.
			if (!_watchlistSynced || accountId != _watchlistSyncedAccountId)
				return;

			var symbolIds = _watchlistAccess.Watchlist.Select(q => q.SymbolId).ToList();
			var json = JsonSerializer.Serialize(symbolIds);
			if (json == _lastPersistedWatchlistJson)
				return;

			_lastPersistedWatchlistJson = json;
			await _localStorage.SetItemAsync(WatchlistStorageKey(accountId), symbolIds);
		}

		// Re-subscribes whatever this browser had watched for the account now selected -- each
		// account keeps its own watchlist in storage, so this reruns on every account change.
		private async Task SyncAccountWatchlistAsync()
		{
			var accountId = _watchlistAccess.SelectedAccountId;
			if (_watchlistSynced && accountId == _watchlistSyncedAccountId)
				return;

			_watchlistSynced = true;
			_watchlistSyncedAccountId = accountId;
			_lastPersistedWatchlistJson = null;

			if (accountId is not { } id)
				return;

			var symbolIds = await _localStorage.GetItemAsync<List<long>>(WatchlistStorageKey(id)) ?? [];
			foreach (var symbolId in symbolIds)
			{
				if (_watchlistAccess.IsSymbolWatched(symbolId))
					continue;

				await _watchlistAccess.WatchSymbolAsync(symbolId);
				await _metadataCache.EnsureAsync(symbolId);
			}

			_lastPersistedWatchlistJson = JsonSerializer.Serialize(symbolIds);
		}

		private void OnSessionChanged() => _ = HandleSessionChangedAsync();

		// Session and its Positions are a long-lived singleton, so a reconnect that doesn't fire
		// Changed still needs positions' metadata ensured -- mirrors TradePage's own first-render check.
		private async Task HandleSessionChangedAsync()
		{
			if (_restorePending && IsConnected)
				ApplyPendingRestore();

			if (IsConnected)
				await SyncAccountWatchlistAsync();
			else
				_watchlistSynced = false;

			await _metadataCache.EnsureForPositionsAsync(_tickStreamAccess.Positions);
			Changed?.Invoke();
		}

		public void Dispose()
		{
			_watchlistAccess.Changed -= OnSessionChanged;
		}
	}
}
