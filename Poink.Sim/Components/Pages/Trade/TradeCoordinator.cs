using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
	/// their own render context themselves (see TradePage.OnCoordinatorChanged).
	/// </summary>
	internal sealed class TradeCoordinator : ITradeCoordinator, IDisposable
	{
		private const string SettingsStorageKey = "poink.trade.setup";

		private readonly IWatchlistAccess _watchlistAccess;
		private readonly ITickStreamAccess _tickStreamAccess;
		private readonly ISymbolMetadataCache _metadataCache;
		private readonly ILocalStorageHelper _localStorage;

		private string? _lastPersistedSettingsJson;
		private bool _restorePending;
		private List<long> _pendingWatchSymbolIds = [];
		private long? _pendingChartSymbolId;
		private string? _pendingChartSymbolName;

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

		// Unwatching the symbol whose chart is open closes that chart too, since a chart
		// with no live quote behind it can no longer receive ticks.
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
				PendingChartSettings = new ChartPanelSettings(
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
				SidepanelCollapsed = settings.SidepanelCollapsed;
				RightPanelCollapsed = settings.RightPanelCollapsed;
				_pendingChartSymbolId = settings.ChartSymbolId;
				_pendingChartSymbolName = settings.ChartSymbolName;
				_pendingWatchSymbolIds = settings.WatchedSymbolIds;
				_restorePending = true;

				if (IsConnected)
					await ApplyPendingRestoreAsync();
			}

			// Session and its Positions are a long-lived singleton, so navigating back to an already-
			// connected session raises no Changed event to trigger this -- check once on mount too.
			if (IsConnected)
				await _metadataCache.EnsureForPositionsAsync(_tickStreamAccess.Positions);
		}

		public async Task PersistSettingsIfChangedAsync(ChartPanelSettings? chartSettings)
		{
			var settings = BuildSettings(chartSettings);
			var json = JsonSerializer.Serialize(settings);
			if (json == _lastPersistedSettingsJson)
				return;

			_lastPersistedSettingsJson = json;
			await _localStorage.SetItemAsync(SettingsStorageKey, settings);
		}

		private TradePageSettings BuildSettings(ChartPanelSettings? chart) =>
			new(
				BarSizeSeconds,
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
				ChartSymbolId,
				ChartSymbolName,
				[.. _watchlistAccess.Watchlist.Select(q => q.SymbolId)],
				SidepanelCollapsed,
				RightPanelCollapsed
			);

		// Re-subscribes whatever this browser had watched and reopens its last chart -- called once
		// settings load if already connected, or on the next Connected transition otherwise.
		private async Task ApplyPendingRestoreAsync()
		{
			if (!_restorePending)
				return;
			_restorePending = false;

			foreach (var symbolId in _pendingWatchSymbolIds)
			{
				if (_watchlistAccess.IsSymbolWatched(symbolId))
					continue;

				await _watchlistAccess.WatchSymbolAsync(symbolId);
				await _metadataCache.EnsureAsync(symbolId);
			}

			if (_pendingChartSymbolId is { } id && _pendingChartSymbolName is { } name && ChartSymbolId is null)
				OpenChart(id, name);
		}

		private void OnSessionChanged() => _ = HandleSessionChangedAsync();

		// Session and its Positions are a long-lived singleton, so a reconnect that doesn't fire
		// Changed still needs positions' metadata ensured -- mirrors TradePage's own first-render check.
		private async Task HandleSessionChangedAsync()
		{
			if (_restorePending && IsConnected)
				await ApplyPendingRestoreAsync();

			await _metadataCache.EnsureForPositionsAsync(_tickStreamAccess.Positions);
			Changed?.Invoke();
		}

		public void Dispose()
		{
			_watchlistAccess.Changed -= OnSessionChanged;
		}
	}
}
