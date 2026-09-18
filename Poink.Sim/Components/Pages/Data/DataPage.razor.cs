using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.Import;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Data
{
	public partial class DataPage : ComponentBase, IDisposable
	{
		private enum Tab
		{
			Registry,
			Symbols,
			Log,
			Import,
			Daily,
		}

		[Inject]
		private ITickIngest Ingest { get; set; } = null!;

		[Inject]
		private IZTicksImportService ZTicksImport { get; set; } = null!;

		[Inject]
		private ICTraderSessionService Session { get; set; } = null!;

		[Inject]
		private ISymbolService SymbolService { get; set; } = null!;

		[Inject]
		private ISymbolMetadataCache MetadataCache { get; set; } = null!;

		[Inject]
		private ILogger<DataPage> Logger { get; set; } = null!;

		private Tab _activeTab = Tab.Registry;

		private Timer? _refreshTimer;

		private IReadOnlyList<TickTableStatRow> _registry = Array.Empty<TickTableStatRow>();
		private string? _selectedRegistryTable;
		private IReadOnlyList<BackfillLogEntry> _backfillLog = Array.Empty<BackfillLogEntry>();
		private string _logSymbolFilter = string.Empty;

		private IReadOnlyList<SymbolModel> _symbols = Array.Empty<SymbolModel>();
		private bool _symbolsBusy;
		private string? _symbolsResult;

		private readonly HashSet<long> _metadataBusy = new();

		private string _importCachePath = string.Empty;
		private string _importSymbol = string.Empty;
		private bool _importIsLive;
		private bool _importCreateTable = true;
		private IReadOnlyList<ZTicksPreviewEntry> _importPreview = Array.Empty<ZTicksPreviewEntry>();
		private bool _importScanBusy;
		private string? _scanResult;
		private bool _showFolderBrowser;

		private string _dailyStatsTable = string.Empty;
		private IReadOnlyList<TickDailyStatRow> _dailyStats = Array.Empty<TickDailyStatRow>();

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private string? SelectedRegistrySymbol => _registry.FirstOrDefault(r => r.Table == _selectedRegistryTable)?.Symbol;

		private IReadOnlyList<BackfillLogEntry> FilteredLog =>
			string.IsNullOrWhiteSpace(_logSymbolFilter)
				? _backfillLog
				: _backfillLog
					.Where(e => e.SymbolName.Contains(_logSymbolFilter, StringComparison.OrdinalIgnoreCase))
					.ToList();

		protected override void OnInitialized()
		{
			Session.Changed += OnChanged;
			ZTicksImport.Changed += OnImportChanged;
			_refreshTimer = new Timer(_ => OnTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
			_registry = Ingest.GetRegistry();
		}

		private void SelectRegistryEntry(TickTableStatRow row) =>
			_selectedRegistryTable = _selectedRegistryTable == row.Table ? null : row.Table;

		private void OnTick()
		{
			try
			{
				if (_activeTab == Tab.Registry)
					_registry = Ingest.GetRegistry();
				else if (_activeTab == Tab.Log)
					_backfillLog = Ingest.GetBackfillLog();
				else if (_activeTab == Tab.Daily && !string.IsNullOrEmpty(_dailyStatsTable))
					_dailyStats = Ingest.GetDailyStats(_dailyStatsTable);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Data page refresh tick failed.");
			}
			OnChanged();
		}

		// Fires from ZTicksImport's own background task, possibly while this page isn't the active tab.
		private void OnImportChanged()
		{
			if (!ZTicksImport.IsRunning)
			{
				_registry = Ingest.GetRegistry();
				if (!string.IsNullOrEmpty(_dailyStatsTable))
					_dailyStats = Ingest.GetDailyStats(_dailyStatsTable);
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
			else if (tab == Tab.Daily)
			{
				_registry = Ingest.GetRegistry();
				if (string.IsNullOrEmpty(_dailyStatsTable) && _registry.Count > 0)
					_dailyStatsTable = _registry[0].Table;
				if (!string.IsNullOrEmpty(_dailyStatsTable))
					_dailyStats = Ingest.GetDailyStats(_dailyStatsTable);
			}
		}

		private void SelectDailyStatsTable(string table)
		{
			_dailyStatsTable = table;
			_dailyStats = string.IsNullOrEmpty(table) ? Array.Empty<TickDailyStatRow>() : Ingest.GetDailyStats(table);
		}

		private static int CountGaps(IReadOnlyList<TickDailyStatRow> rows)
		{
			var gaps = 0;
			for (var i = 1; i < rows.Count; i++)
			{
				if (rows[i].Day.DayNumber - rows[i - 1].Day.DayNumber > 1)
					gaps++;
			}
			return gaps;
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

		private SymbolMetadataModel? GetMetadata(long symbolId) => MetadataCache.Get(symbolId);

		private bool IsMetadataBusy(long symbolId) => _metadataBusy.Contains(symbolId);

		private async Task GetMetadataAsync(long symbolId)
		{
			if (!IsConnected || !_metadataBusy.Add(symbolId))
				return;

			OnChanged();
			Logger.LogInformation("Get Metadata started for symbol {SymbolId}.", symbolId);
			try
			{
				await MetadataCache.EnsureAsync(symbolId);
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

		private async Task ScanImportAsync()
		{
			if (_importScanBusy || string.IsNullOrWhiteSpace(_importCachePath))
				return;

			_importScanBusy = true;
			_scanResult = null;
			_importPreview = Array.Empty<ZTicksPreviewEntry>();
			OnChanged();
			Logger.LogInformation("ZTicks scan started for {Path}.", _importCachePath);
			try
			{
				_importPreview = await ZTicksImport.PreviewAsync(_importCachePath);
				_scanResult = $"Found {_importPreview.Count:N0} file(s), {_importPreview.Sum(e => (long)e.TickCount):N0} tick(s).";
				Logger.LogInformation("ZTicks scan finished: {Count} file(s).", _importPreview.Count);
			}
			catch (Exception ex)
			{
				_scanResult = $"Scan failed: {ex.Message}";
				Logger.LogError(ex, "ZTicks scan failed for {Path}.", _importCachePath);
			}
			finally
			{
				_importScanBusy = false;
				OnChanged();
			}
		}

		private void StartImport()
		{
			if (_importPreview.Count == 0 || string.IsNullOrWhiteSpace(_importSymbol))
				return;

			var request = new ZTicksImportRequest(
				_importCachePath,
				_importSymbol.Trim(),
				_importIsLive,
				_importCreateTable,
				ResolveSymbol(_importSymbol)?.SymbolId ?? 0
			);

			if (!ZTicksImport.StartImport(request))
				return;

			Logger.LogInformation(
				"ZTicks import started: {Symbol} ({Env}) from {Path}.",
				request.SymbolName,
				request.IsLive ? "live" : "demo",
				request.CachePath
			);
		}

		private void CancelImport() => ZTicksImport.CancelImport();

		private void OpenFolderBrowser()
		{
			_showFolderBrowser = true;
			OnChanged();
		}

		private void CloseFolderBrowser()
		{
			_showFolderBrowser = false;
			OnChanged();
		}

		private void OnFolderSelected(string path)
		{
			_importCachePath = path;
			_showFolderBrowser = false;
			OnChanged();
		}

		private string FolderBrowserStartPath()
		{
			if (Directory.Exists(_importCachePath))
				return _importCachePath;
			if (File.Exists(_importCachePath))
				return Path.GetDirectoryName(_importCachePath) ?? DefaultBrowseRoot();
			return DefaultBrowseRoot();
		}

		// The cache always lives under this Spotware folder once cTrader has run a backtest, so start there.
		private static string DefaultBrowseRoot()
		{
			var spotwareCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotware", "Cache");
			return Directory.Exists(spotwareCache) ? spotwareCache : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}

		private static string Time(DateTime? utc) =>
			utc?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "—";

		private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

		private static double ImportPercent(ZTicksImportProgress? progress) =>
			progress is { FilesTotal: > 0 } p ? p.FilesDone * 100.0 / p.FilesTotal : 0;

		private static string Percent(double value) => value.ToString("F0", CultureInfo.InvariantCulture);

		private void OnChanged() => InvokeAsync(StateHasChanged);

		public void Dispose()
		{
			Session.Changed -= OnChanged;
			ZTicksImport.Changed -= OnImportChanged;
			_refreshTimer?.Dispose();
		}
	}
}
