using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Data
{
	public partial class DataPage : ComponentBase, IDisposable
	{
		private const int MaxSymbolResults = 20;
		private const int DefaultLoadHistoricDays = 1;

		private enum Tab
		{
			Ingest,
			Registry,
			Symbols,
			Log,
		}

		[Inject]
		private ITickBackfillController Backfill { get; set; } = null!;

		[Inject]
		private ITickIngest Ingest { get; set; } = null!;

		[Inject]
		private ICTraderSessionService Session { get; set; } = null!;

		[Inject]
		private ISymbolService SymbolService { get; set; } = null!;

		[Inject]
		private ILogger<DataPage> Logger { get; set; } = null!;

		private Tab _activeTab = Tab.Ingest;

		private string _symbolFilter = string.Empty;
		private bool _createTable = true;
		private Timer? _refreshTimer;

		private readonly Dictionary<string, LoadHistoricState> _loadHistoric = new();

		private IReadOnlyList<TickTableStatRow> _registry = Array.Empty<TickTableStatRow>();
		private IReadOnlyList<BackfillLogEntry> _backfillLog = Array.Empty<BackfillLogEntry>();
		private string _logSymbolFilter = string.Empty;

		private IReadOnlyList<SymbolModel> _symbols = Array.Empty<SymbolModel>();
		private bool _symbolsBusy;
		private string? _symbolsResult;

		private readonly Dictionary<long, SymbolMetadataModel> _metadataCache = new();
		private readonly HashSet<long> _metadataBusy = new();

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private IReadOnlyList<SymbolIngestStatus> Statuses => Backfill.GetStatuses();

		private IReadOnlyList<CTraderSymbolInfo> FilteredSymbols => Filter(_symbolFilter);

		private IReadOnlyList<BackfillLogEntry> FilteredLog =>
			string.IsNullOrWhiteSpace(_logSymbolFilter)
				? _backfillLog
				: _backfillLog
					.Where(e => e.SymbolName.Contains(_logSymbolFilter, StringComparison.OrdinalIgnoreCase))
					.ToList();

		private IReadOnlyList<CTraderSymbolInfo> Filter(string term) =>
			string.IsNullOrWhiteSpace(term)
				? Array.Empty<CTraderSymbolInfo>()
				: Session
					.Symbols.Where(s => s.SymbolName.Contains(term, StringComparison.OrdinalIgnoreCase))
					.Take(MaxSymbolResults)
					.ToList();

		protected override void OnInitialized()
		{
			Backfill.Changed += OnChanged;
			Session.Changed += OnChanged;
			_refreshTimer = new Timer(_ => OnTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
		}

		private void OnTick()
		{
			try
			{
				if (_activeTab == Tab.Registry)
					_registry = Ingest.GetRegistry();
				else if (_activeTab == Tab.Log)
					_backfillLog = Ingest.GetBackfillLog();
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Data page refresh tick failed.");
			}
			OnChanged();
		}

		private void SwitchTab(Tab tab)
		{
			_activeTab = tab;
			if (tab == Tab.Registry)
				_registry = Ingest.GetRegistry();
			else if (tab == Tab.Log)
				_backfillLog = Ingest.GetBackfillLog();
			else if (tab == Tab.Symbols)
				_ = LoadSymbolsAsync();
		}

		private async Task LoadSymbolsAsync()
		{
			_symbols = await SymbolService.ListAsync();
			OnChanged();
		}

		private async Task GetSymbolsAsync()
		{
			if (_symbolsBusy || !IsConnected)
				return;

			_symbolsBusy = true;
			_symbolsResult = null;
			OnChanged();
			Logger.LogInformation("Get Symbols started.");
			try
			{
				_symbols = await SymbolService.RefreshFromCTraderAsync();
				_symbolsResult = $"Saved {_symbols.Count:N0} symbol(s).";
				Logger.LogInformation("Get Symbols finished: {Count} symbol(s) saved.", _symbols.Count);
			}
			catch (Exception ex)
			{
				_symbolsResult = $"Failed: {ex.Message}";
				Logger.LogError(ex, "Get Symbols failed.");
			}
			finally
			{
				_symbolsBusy = false;
				OnChanged();
			}
		}

		private SymbolMetadataModel? GetMetadata(long symbolId) =>
			_metadataCache.TryGetValue(symbolId, out var metadata) ? metadata : null;

		private bool IsMetadataBusy(long symbolId) => _metadataBusy.Contains(symbolId);

		private async Task GetMetadataAsync(long symbolId)
		{
			if (!IsConnected || !_metadataBusy.Add(symbolId))
				return;

			OnChanged();
			Logger.LogInformation("Get Metadata started for symbol {SymbolId}.", symbolId);
			try
			{
				var metadata = await SymbolService.RefreshMetadataFromCTraderAsync(symbolId);
				if (metadata is not null)
					_metadataCache[symbolId] = metadata;
				Logger.LogInformation("Get Metadata finished for symbol {SymbolId}.", symbolId);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Get Metadata failed for symbol {SymbolId}.", symbolId);
			}
			finally
			{
				_metadataBusy.Remove(symbolId);
				OnChanged();
			}
		}

		private List<SymbolRow> Rows =>
			_symbols
				.Select(s => new SymbolRow(
					s.SymbolId,
					s.SymbolName,
					s.Enabled,
					s.BaseAssetId,
					s.QuoteAssetId,
					s.SymbolCategoryId,
					s.Description,
					s.SortingNumber,
					s.CreatedAt,
					s.ChangedAt
				))
				.ToList();

		// Flattens SymbolModel into a DataTable-friendly shape, matching its reflected-property display.
		private sealed record SymbolRow(
			long SymbolId,
			string SymbolName,
			bool Enabled,
			long BaseAssetId,
			long QuoteAssetId,
			long SymbolCategoryId,
			string Description,
			double SortingNumber,
			DateTime CreatedAt,
			DateTime? ChangedAt
		);

		private async Task TrackAndStreamAsync(CTraderSymbolInfo symbol)
		{
			Backfill.Track(symbol.SymbolId, symbol.SymbolName, _createTable);
			if (IsConnected)
				await Session.WatchSymbolAsync(symbol.SymbolId);
		}

		private async Task StopTrackingAsync(SymbolIngestStatus status)
		{
			Backfill.Untrack(status.SymbolId);
			if (IsConnected)
				await Session.UnwatchSymbolAsync(status.SymbolId);
		}

		private LoadHistoricState GetHistoricState(string table)
		{
			if (!_loadHistoric.TryGetValue(table, out var state))
			{
				state = new LoadHistoricState { Days = DefaultLoadHistoricDays };
				_loadHistoric[table] = state;
			}
			return state;
		}

		private void UpdateRegistrySymbolAsync(string table, string symbolName)
		{
			var trimmed = symbolName.Trim();
			var current = _registry.FirstOrDefault(r => r.Table == table)?.Symbol ?? string.Empty;
			if (trimmed == current)
				return;

			Ingest.AssociateSymbol(table, trimmed);
			_registry = Ingest.GetRegistry();
		}

		private CTraderSymbolInfo? ResolveSymbol(string? symbolCode) =>
			symbolCode is null ? null : Session.Symbols.FirstOrDefault(s => s.SymbolName == symbolCode);

		private async Task LoadHistoricAsync(TickTableStatRow row)
		{
			var state = GetHistoricState(row.Table);
			if (state.Busy || row.MinUtc is not { } to)
				return;

			var symbol = ResolveSymbol(row.Symbol);
			if (symbol is null)
			{
				state.Result = "Symbol not resolved against the connected account's symbol list.";
				return;
			}

			var from = to.AddDays(-Math.Max(1, state.Days));
			state.Busy = true;
			state.Result = null;
			Logger.LogInformation(
				"Load Historic started for {Table} ({Symbol}), {From:u}..{To:u}.",
				row.Table,
				symbol.SymbolName,
				from,
				to
			);
			try
			{
				var written = await Backfill.BackfillRangeAsync(symbol.SymbolId, symbol.SymbolName, from, to);
				state.Result = $"Wrote {written:N0} tick(s) before {Time(to)}.";
				Logger.LogInformation(
					"Load Historic finished for {Table} ({Symbol}): {Count} tick(s) written.",
					row.Table,
					symbol.SymbolName,
					written
				);
				_registry = Ingest.GetRegistry();
			}
			catch (Exception ex)
			{
				state.Result = $"Failed: {ex.Message}";
				Logger.LogError(
					ex,
					"Load Historic failed for {Table} ({Symbol}), {From:u}..{To:u}.",
					row.Table,
					symbol.SymbolName,
					from,
					to
				);
			}
			finally
			{
				state.Busy = false;
			}
		}

		private void ForceFlush(long symbolId) => Backfill.ForceFlush(symbolId);

		private void TogglePause()
		{
			if (Backfill.IsPaused)
				Backfill.Resume();
			else
				Backfill.Pause();
		}

		private bool IsTracked(long symbolId) => Statuses.Any(s => s.SymbolId == symbolId);

		private static string StateLabel(TickIngestState state) =>
			state switch
			{
				TickIngestState.NoTable => "NO TABLE",
				TickIngestState.Idle => "IDLE",
				TickIngestState.ClosingGap => "CLOSING GAP",
				TickIngestState.DrainingBuffer => "DRAINING",
				TickIngestState.TailingLive => "LIVE",
				TickIngestState.Error => "ERROR",
				_ => state.ToString().ToUpperInvariant(),
			};

		private static string StateClass(TickIngestState state) =>
			state switch
			{
				TickIngestState.TailingLive => "data-state-live",
				TickIngestState.DrainingBuffer => "data-state-live",
				TickIngestState.ClosingGap => "data-state-gap",
				TickIngestState.Error => "data-state-error",
				TickIngestState.NoTable => "data-state-error",
				_ => "data-state-idle",
			};

		private static string HistoricStateLabel(LoadHistoricState state) =>
			state.Busy ? "RUNNING"
			: state.Result is null ? "IDLE"
			: state.Result.StartsWith("Failed", StringComparison.Ordinal) ? "FAILED"
			: "DONE";

		private static string HistoricStateClass(LoadHistoricState state) =>
			state.Busy ? "data-state-live"
			: state.Result is null ? "data-state-idle"
			: state.Result.StartsWith("Failed", StringComparison.Ordinal) ? "data-state-error"
			: "data-state-live";

		private static string Time(DateTime? utc) =>
			utc?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "—";

		private static string Lag(double? seconds) =>
			seconds switch
			{
				null => "—",
				< 60 => $"{seconds.Value:F0}s",
				< 3600 => $"{seconds.Value / 60:F0}m",
				_ => $"{seconds.Value / 3600:F1}h",
			};

		private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

		private void OnChanged() => InvokeAsync(StateHasChanged);

		public void Dispose()
		{
			Backfill.Changed -= OnChanged;
			Session.Changed -= OnChanged;
			_refreshTimer?.Dispose();
		}

		private sealed class LoadHistoricState
		{
			public int Days { get; set; }
			public bool Busy { get; set; }
			public string? Result { get; set; }
		}
	}
}
