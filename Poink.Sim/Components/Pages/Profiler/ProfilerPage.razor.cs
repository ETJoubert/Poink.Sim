using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Profiler;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages
{
	public partial class ProfilerPage : ComponentBase
	{
		[Inject]
		private IProfilerService Service { get; set; } = default!;

		[Inject]
		private ITRangeChartRepository TRangeChartRepository { get; set; } = default!;

		[Inject]
		private ITRangeReferenceChartRepository TRangeReferenceChartRepository { get; set; } = default!;

		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Inject]
		private ISymbolTableResolver SymbolTableResolver { get; set; } = default!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		[Inject]
		private ILogger<ProfilerPage> Logger { get; set; } = default!;

		private const int BarsPerFetch = 300;

		private const string SettingsStorageKey = ProfilerPageSettings.StorageKey;

		private string? _lastPersistedSettingsJson;

		// Gates TickDataSource too, so it can't seed its default table over a restored symbol selection.
		private bool _settingsLoaded;

		private Dictionary<string, double> _setHeights = new();

		private string _table = "";

		// Ordered selection from the symbols panel; the first entry is the primary symbol that _table mirrors.
		private List<string> _selectedTables = new();
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;
		private int _barSizeSeconds = 60;
		private BarAggregationKind _barAggregationKind = BarAggregationKind.Ohlc;

		// Cleared after the first range settle so later manual range edits still require LOAD.
		private bool _autoLoadPending = true;

		private bool _isLoading;
		private string? _error;

		private PanelTab _activePanelTab;
		private bool _rightPanelCollapsed = true;

		private enum PanelTab
		{
			Drawings,
			SavedTRangeCharts,
			SavedTRangeReferenceCharts,
		}

		private ProfilerConfig? _config;

		// One per symbol, primary first; comparison sets follow the selection order and load as soon as they're added.
		private List<ProfilerSymbolData> _sets = new();

		private sealed record Drawing(
			List<ChartHorizontalLine> HorizontalLines,
			List<ChartVerticalLine> VerticalLines,
			List<ChartDiagonalLine> DiagonalLines,
			List<CTraderBarRow> ChartBars,
			List<ChartVerticalLine> ReferenceLines
		)
		{
			public int Id { get; init; }
			public int? RangeBarCount { get; init; }
			public TimeSpan? RangeSpan { get; init; }

			public int? ReferenceBarCount { get; init; }

			public string Kind =>
				this switch
				{
					{ ChartBars.Count: > 0, ReferenceLines.Count: > 0 } => "T-RANGE REFERENCE CHART",
					{ ChartBars.Count: > 0 } => "T-RANGE CHART",
					{ HorizontalLines.Count: 1, VerticalLines.Count: 0, DiagonalLines.Count: 0 } => "H-LINE",
					{ HorizontalLines.Count: 0, VerticalLines.Count: 1, DiagonalLines.Count: 0 } => "V-LINE",
					{ HorizontalLines.Count: 0, VerticalLines.Count: 0, DiagonalLines.Count: 1 } => "D-LINE",
					{ HorizontalLines.Count: 2, VerticalLines.Count: 0 } => "P-RANGE",
					{ HorizontalLines.Count: 0, VerticalLines.Count: 2 } => "T-RANGE",
					{ HorizontalLines.Count: 2, VerticalLines.Count: 2 } => "RECTANGLE",
					_ => "DRAWING",
				};

			public string? ColorGroup =>
				Kind switch
				{
					"H-LINE" or "V-LINE" or "D-LINE" => "lines",
					"T-RANGE" or "P-RANGE" => "ranges",
					"RECTANGLE" => "shapes",
					"T-RANGE CHART" or "T-RANGE REFERENCE CHART" => "charts",
					_ => null,
				};

			public bool IsRange =>
				VerticalLines.Count == 2 && Kind is "T-RANGE" or "T-RANGE CHART" or "T-RANGE REFERENCE CHART";
		}

		private List<Drawing> _drawings = new();
		private Drawing _pending = NewPending();
		private int _nextDrawingId = 1;

		private DateTime? _volumeRangeStart;
		private DateTime? _volumeRangeEnd;

		private bool _lookbackEnabled;
		private int _lookbackWidth = 50;
		private int _lookbackOffset;

		private IndicatorSettings _indicators = new();

		private bool _showMainChart = true;
		private bool _showXGrid = true;
		private bool _showVolumeProfile = false;
		private bool _showTimeProfile = false;
		private bool _showSubchart = false;
		private bool _showSnapshot = false;

		private int _subchartBarSizeSeconds = 10;
		private BarAggregationKind _subchartBarKind = BarAggregationKind.Ohlc;

		private int _xgridColCount = 20;

		private HashSet<int> _selectedDrawingIds = new();
		private HashSet<int> _savedDrawingIds = new();
		private bool _showLabelInput;
		private string _labelText = "";

		private List<TRangeChart> _savedTRangeCharts = new();

		private List<TRangeReferenceChart> _savedTRangeReferenceCharts = new();

		private readonly Dictionary<long, List<CTraderBarRow>> _chartBarsCache = new();
		private readonly HashSet<long> _chartBarsLoading = new();

		private readonly Dictionary<long, List<CTraderBarRow>> _referenceChartBarsCache = new();
		private readonly HashSet<long> _referenceChartBarsLoading = new();

		private static Drawing NewPending() => new(new(), new(), new(), new(), new());

		private IEnumerable<Drawing> AllDrawings() => _drawings.Append(_pending);

		protected override void OnInitialized()
		{
			_savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
			_savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				await LoadPersistedSettingsAsync();
				await LoadRestoredSelectionAsync();
				return;
			}

			await PersistSettingsIfChangedAsync();
		}

		private ProfilerPageSettings CurrentSettings =>
			new(
				_from,
				_to,
				_selectedTables,
				_barSizeSeconds,
				_barAggregationKind,
				_indicators,
				_lookbackEnabled,
				_lookbackWidth,
				_lookbackOffset,
				_showMainChart,
				_showXGrid,
				_showVolumeProfile,
				_showTimeProfile,
				_showSubchart,
				_showSnapshot,
				_subchartBarSizeSeconds,
				_subchartBarKind,
				_xgridColCount,
				_rightPanelCollapsed,
				_setHeights
			);

		private async Task LoadPersistedSettingsAsync()
		{
			try
			{
				if (await LocalStorage.GetItemAsync<ProfilerPageSettings>(SettingsStorageKey) is { } settings)
				{
					ApplySettings(settings);
					Logger.LogInformation(
						"Profiler settings restored for [{Tables}] from {From} to {To} at {BarSize}s bars",
						string.Join(", ", _selectedTables),
						_from,
						_to,
						_barSizeSeconds
					);
				}
				_lastPersistedSettingsJson = JsonSerializer.Serialize(CurrentSettings);
			}
			finally
			{
				_settingsLoaded = true;
			}
			StateHasChanged();
		}

		private void ApplySettings(ProfilerPageSettings settings)
		{
			_from = settings.From;
			_to = settings.To;
			_selectedTables = settings.SelectedTables?.Distinct().ToList() ?? new();
			_table = _selectedTables.FirstOrDefault() ?? "";
			_barSizeSeconds = settings.BarSizeSeconds;
			_barAggregationKind = settings.BarAggregationKind;
			_indicators = settings.Indicators ?? new();
			_lookbackEnabled = settings.LookbackEnabled;
			_lookbackWidth = Math.Max(1, settings.LookbackWidth);
			_lookbackOffset = Math.Max(0, settings.LookbackOffset);
			_showMainChart = settings.ShowMainChart;
			_showXGrid = settings.ShowXGrid;
			_showVolumeProfile = settings.ShowVolumeProfile;
			_showTimeProfile = settings.ShowTimeProfile;
			_showSubchart = settings.ShowSubchart;
			_showSnapshot = settings.ShowSnapshot;
			_subchartBarSizeSeconds = settings.SubchartBarSizeSeconds;
			_subchartBarKind = settings.SubchartBarKind;
			_xgridColCount = settings.XGridColCount;
			_rightPanelCollapsed = settings.RightPanelCollapsed;
			_setHeights = settings.SetHeights ?? new();
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

		// Stands in for the first-visit auto-load, which only fires when TickDataSource seeds a default table.
		private async Task LoadRestoredSelectionAsync()
		{
			if (string.IsNullOrWhiteSpace(_table))
				return;

			_autoLoadPending = false;
			var loading = Load();
			StateHasChanged();
			await loading;

			if (_lookbackEnabled)
				ApplyLookbackWindow();
			StateHasChanged();
		}

		private double? SetHeight(string table) => _setHeights.TryGetValue(table, out var height) ? height : null;

		private void OnSetHeightChanged(string table, double? height)
		{
			if (height is { } resized)
				_setHeights[table] = resized;
			else
				_setHeights.Remove(table);
		}

		private ProfilerSymbolData? PrimarySet => _sets.Count > 0 ? _sets[0] : null;

		private List<HlocAvdtBar> PrimaryBars => PrimarySet?.Bars ?? new();

		private bool HasData => _config is not null && PrimarySet is { HasBars: true };

		private async Task Load()
		{
			_error = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_error = "add a symbol in the SYMBOLS panel";
				return;
			}
			if (_to <= _from)
			{
				_error = "To must be after From";
				return;
			}

			_isLoading = true;
			_config = null;
			_sets = new();
			_drawings = new();
			_pending = NewPending();
			_nextDrawingId = 1;
			_selectedDrawingIds.Clear();
			_savedDrawingIds.Clear();
			_volumeRangeStart = null;
			_volumeRangeEnd = null;
			CancelLabelSelected();

			try
			{
				var config = new ProfilerConfig(_table, _from, _to, TimeSpan.FromSeconds(_barSizeSeconds));
				Logger.LogInformation(
					"Profiler load started for [{Tables}] from {From} to {To} at {BarSize} bars",
					string.Join(", ", _selectedTables),
					config.From,
					config.To,
					config.BarSize
				);
				var chunk = await Service.LoadAsync(config, config.To, BarsPerFetch);
				if (chunk.Bars.Count == 0 && chunk.ReachedStart)
				{
					_error = "no ticks in the selected range";
					Logger.LogWarning("Profiler load found no ticks for primary symbol {Table}", config.Table);
				}
				else
				{
					_config = config;
					var primary = new ProfilerSymbolData(config.Table);
					primary.ApplyChunk(chunk, _barAggregationKind);
					_ = ResolvePipSizeAsync(primary);
					_sets = [primary];
					SyncComparisonSets();
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Profiler load failed for primary symbol {Table}", _table);
				_error = ex.Message;
			}
			finally
			{
				_isLoading = false;
			}
		}

		// Keeps the primary set until the next LOAD and adds/drops comparison sets to match the selection right away.
		private void SyncComparisonSets()
		{
			if (_config is null || PrimarySet is not { } primary)
				return;

			var existing = _sets.Skip(1).ToDictionary(s => s.Table);
			var comparisons = _selectedTables
				.Where(t => t != primary.Table)
				.Select(t => existing.TryGetValue(t, out var set) ? set : StartComparisonLoad(t, _config));
			_sets = comparisons.Prepend(primary).ToList();
		}

		private ProfilerSymbolData StartComparisonLoad(string table, ProfilerConfig primaryConfig)
		{
			var set = new ProfilerSymbolData(table) { IsLoading = true };
			_ = LoadComparisonAsync(set, primaryConfig with { Table = table });
			_ = ResolvePipSizeAsync(set);
			return set;
		}

		private async Task ResolvePipSizeAsync(ProfilerSymbolData set)
		{
			try
			{
				var resolution = await SymbolTableResolver.ResolveAsync(set.Table);
				if (resolution.Limits is { } limits)
				{
					set.PipSize = limits.PipSize;
					Logger.LogInformation("Profiler pip size for {Table} resolved to {PipSize}", set.Table, limits.PipSize);
				}
				else
				{
					Logger.LogWarning(
						"No trading metadata for {Table}; Profiler XGrid assumes a {PipSize} pip size",
						set.Table,
						ProfilerSymbolData.FallbackPipSize
					);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Could not resolve the pip size for {Table}; Profiler XGrid assumes a {PipSize} pip size", set.Table, ProfilerSymbolData.FallbackPipSize);
			}
			await InvokeAsync(StateHasChanged);
		}

		private async Task LoadComparisonAsync(ProfilerSymbolData set, ProfilerConfig config)
		{
			try
			{
				var chunk = await Service.LoadAsync(config, config.To, BarsPerFetch);
				if (chunk.Bars.Count == 0 && chunk.ReachedStart)
				{
					set.Error = "no ticks in the selected range";
					Logger.LogWarning("Profiler load found no ticks for comparison symbol {Table}", config.Table);
				}
				else
				{
					set.ApplyChunk(chunk, _barAggregationKind);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Profiler load failed for comparison symbol {Table}", config.Table);
				set.Error = ex.Message;
			}
			finally
			{
				set.IsLoading = false;
			}
			await InvokeAsync(StateHasChanged);
		}

		private async Task LoadMore(ProfilerSymbolData set)
		{
			// Latched before the guard since OnNearStart re-renders even on an early return, and a stale false re-fits the chart.
			set.KeepView = true;

			if (_config is null || set.ReachedStart || set.IsLoadingMore)
				return;
			set.IsLoadingMore = true;

			try
			{
				var config = _config with { Table = set.Table };
				var cursor = set.HasBars ? set.Bars[0].OpenTime : config.To;
				var chunk = await Service.LoadAsync(config, cursor, BarsPerFetch);
				set.ApplyChunk(chunk, _barAggregationKind);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Profiler backfill failed for {Table}", set.Table);
				set.Error = ex.Message;
			}
			finally
			{
				set.IsLoadingMore = false;
			}
		}

		private void RefreshBarRows()
		{
			foreach (var set in _sets)
				set.RefreshBarRows(_barAggregationKind);
		}

		// TickDataSource only reports a table when it defaults one on first visit, which seeds an empty selection.
		private void OnDefaultTableSelected(string table)
		{
			if (_selectedTables.Count == 0 && !string.IsNullOrWhiteSpace(table))
				OnSelectedTablesChanged([table]);
		}

		private void OnSelectedTablesChanged(IReadOnlyList<string> tables)
		{
			_selectedTables = tables.ToList();
			Logger.LogInformation("Profiler symbol selection set to [{Tables}]", string.Join(", ", _selectedTables));
			SyncComparisonSets();

			var primary = _selectedTables.FirstOrDefault() ?? "";
			if (primary == _table)
				return;
			_table = primary;
			SnapRangeIfOutside(primary);
		}

		private void MakePrimary(string table) =>
			OnSelectedTablesChanged(_selectedTables.Where(t => t != table).Prepend(table).ToList());

		private void RemoveSymbol(string table) => OnSelectedTablesChanged(_selectedTables.Where(t => t != table).ToList());

		// Keeps the shared timespan across primary changes unless it holds none of the new primary's ticks.
		private void SnapRangeIfOutside(string table)
		{
			var meta = TickRepository.CachedTables.FirstOrDefault(t => t.Table == table);
			if (meta.MaxTime is not { } maxTime || TickTableRangeHelper.Overlaps(_from, _to, meta.MinTime, maxTime))
				return;

			(_from, _to) = TickTableRangeHelper.DefaultRange(meta.MinTime, maxTime);
			Logger.LogInformation("Profiler range snapped to {From} - {To} for primary symbol {Table}", _from, _to, table);
		}

		private async Task OnRangeSettled()
		{
			if (!_autoLoadPending || string.IsNullOrWhiteSpace(_table))
				return;

			_autoLoadPending = false;
			await Load();
		}

		private void AddHorizontalLine(double price) =>
			_pending.HorizontalLines.Add(new ChartHorizontalLine(price, price.ToString("F5")));

		private void AddVerticalLine(DateTime timeUtc) =>
			_pending.VerticalLines.Add(new ChartVerticalLine(timeUtc, timeUtc.ToString("yyyy-MM-dd HH:mm")));

		private void AddDiagonalLine(ChartDiagonalLine line)
		{
			var label =
				$"{line.StartPrice:F5} @ {line.StartTimeUtc:HH:mm} -> {line.EndPrice:F5} @ {line.EndTimeUtc:HH:mm}";
			_pending.DiagonalLines.Add(line with { Label = label });
		}

		private void AddTRangeChart(ChartTimeRange range)
		{
			var start = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
			var end = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
			var sliceStart = _pending.ReferenceLines.Count > 0 ? _pending.ReferenceLines[0].TimeUtc : start;
			var slice = PrimaryBars.Where(b => b.OpenTime >= sliceStart && b.OpenTime <= end);
			_pending.ChartBars.AddRange(ChartBarRowHelper.ToBarRows(slice));
		}

		private void AddReferenceLine(DateTime timeUtc) =>
			_pending.ReferenceLines.Add(new ChartVerticalLine(timeUtc, timeUtc.ToString("yyyy-MM-dd HH:mm")));

		private void SetVolumeRange(ChartTimeRange range)
		{
			_volumeRangeStart = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
			_volumeRangeEnd = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
			_lookbackEnabled = true;
			SyncLookbackFieldsFromRange();
		}

		private void ClearVolumeRange()
		{
			_volumeRangeStart = null;
			_volumeRangeEnd = null;
			_lookbackEnabled = false;
		}

		private void SetLookbackEnabled(bool enabled)
		{
			_lookbackEnabled = enabled;
			if (_lookbackEnabled)
				ApplyLookbackWindow();
			else
			{
				_volumeRangeStart = null;
				_volumeRangeEnd = null;
			}
		}

		// Width/offset only recompute the applied window while the toggle is on -- otherwise
		// they're just the values it'll use once turned on.
		private int LookbackWidth
		{
			get => _lookbackWidth;
			set
			{
				_lookbackWidth = Math.Max(1, value);
				if (_lookbackEnabled)
					ApplyLookbackWindow();
			}
		}

		private int LookbackOffset
		{
			get => _lookbackOffset;
			set
			{
				_lookbackOffset = Math.Max(0, value);
				if (_lookbackEnabled)
					ApplyLookbackWindow();
			}
		}

		// Resolves _lookbackWidth/_lookbackOffset (both in bars, counted back from the primary symbol's most recently
		// loaded bar) to the shared time window every symbol's chart set reads.
		private void ApplyLookbackWindow()
		{
			if (PrimarySet?.ResolveWindow(_lookbackWidth, _lookbackOffset) is not { } window)
				return;

			_volumeRangeStart = window.Start;
			_volumeRangeEnd = window.End;
		}

		// Keeps the toolbar's width/offset fields in step when the window is instead drawn on
		// a chart via the LOOKBACK WINDOW tool, so neither input method goes stale.
		private void SyncLookbackFieldsFromRange()
		{
			if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end)
				return;
			if (PrimarySet?.WindowInBars(start, end) is not { } bars)
				return;

			_lookbackWidth = bars.Width;
			_lookbackOffset = bars.Offset;
		}

		// Sparkline drag callback, fired continuously while the window's body or either edge is dragged.
		private void OnWindowChanged((int Width, int Offset) window)
		{
			_lookbackWidth = Math.Max(1, window.Width);
			_lookbackOffset = Math.Max(0, window.Offset);
			ApplyLookbackWindow();
		}

		private ProfilerChartSetOptions CurrentSetOptions() =>
			new(
				_showMainChart,
				_showXGrid,
				_showVolumeProfile,
				_showTimeProfile,
				_showSubchart,
				_showSnapshot,
				_indicators,
				_lookbackEnabled,
				_volumeRangeStart,
				_volumeRangeEnd,
				_lookbackWidth,
				_config?.From ?? _from
			);

		private void FinishDrawing()
		{
			if (
				_pending.HorizontalLines.Count > 0
				|| _pending.VerticalLines.Count > 0
				|| _pending.DiagonalLines.Count > 0
			)
			{
				var drawing = _pending with { Id = _nextDrawingId++ };

				if (drawing.VerticalLines.Count == 2)
				{
					var t1 = drawing.VerticalLines[0].TimeUtc;
					var t2 = drawing.VerticalLines[1].TimeUtc;
					var start = t1 <= t2 ? t1 : t2;
					var end = t1 <= t2 ? t2 : t1;
					drawing = drawing with
					{
						RangeBarCount = PrimaryBars.Count(b => b.OpenTime >= start && b.OpenTime <= end),
						RangeSpan = end - start,
					};

					if (drawing.ReferenceLines.Count > 0)
					{
						var referenceStart = drawing.ReferenceLines[0].TimeUtc;
						drawing = drawing with
						{
							ReferenceBarCount = PrimaryBars.Count(b => b.OpenTime >= referenceStart && b.OpenTime < start),
						};
					}
				}

				_drawings = _drawings.Append(drawing).ToList();
			}
			_pending = NewPending();
		}

		private void RemoveDrawing(int index)
		{
			_selectedDrawingIds.Remove(_drawings[index].Id);
			_drawings = _drawings.Where((_, i) => i != index).ToList();
		}

		private void ToggleDrawingSelected(int id)
		{
			if (!_selectedDrawingIds.Remove(id))
				_selectedDrawingIds.Add(id);
		}

		private void RemoveSelectedDrawings()
		{
			_drawings = _drawings.Where(d => !_selectedDrawingIds.Contains(d.Id)).ToList();
			_selectedDrawingIds.Clear();
		}

		private void ShowLabelSelectedInput() => _showLabelInput = true;

		private void CancelLabelSelected()
		{
			_showLabelInput = false;
			_labelText = "";
		}

		private void OnLabelKeyDown(KeyboardEventArgs e)
		{
			if (e.Key == "Enter")
				SaveSelectedDrawings();
			else if (e.Key == "Escape")
				CancelLabelSelected();
		}

		private void SaveSelectedDrawings()
		{
			if (string.IsNullOrWhiteSpace(_labelText) || _config is null || _selectedDrawingIds.Count == 0)
				return;

			var label = _labelText.Trim();
			foreach (
				var drawing in _drawings.Where(d =>
					_selectedDrawingIds.Contains(d.Id) && d.Kind is "T-RANGE CHART" or "T-RANGE REFERENCE CHART"
				)
			)
			{
				var t1 = drawing.VerticalLines[0].TimeUtc;
				var t2 = drawing.VerticalLines[1].TimeUtc;
				var start = t1 <= t2 ? t1 : t2;
				var end = t1 <= t2 ? t2 : t1;
				var barCount = drawing.RangeBarCount ?? drawing.ChartBars.Count - (drawing.ReferenceBarCount ?? 0);

				if (drawing.Kind == "T-RANGE REFERENCE CHART")
				{
					var referenceStart = drawing.ReferenceLines[0].TimeUtc;
					var referenceBarCount = drawing.ReferenceBarCount ?? 0;
					TRangeReferenceChartRepository.SaveTRangeReferenceChart(
						label,
						_config.Table,
						start,
						end,
						_config.BarSize.TotalMinutes,
						barCount,
						referenceStart,
						start,
						referenceBarCount
					);
				}
				else
				{
					TRangeChartRepository.SaveTRangeChart(
						label,
						_config.Table,
						start,
						end,
						_config.BarSize.TotalMinutes,
						barCount
					);
				}

				_savedDrawingIds.Add(drawing.Id);
			}

			_savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
			_savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
			_selectedDrawingIds.Clear();
			CancelLabelSelected();
		}

		private void HandleDeleteTRangeChart(TRangeChart chart)
		{
			TRangeChartRepository.DeleteTRangeChart(chart.Id);
			_savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
			_chartBarsCache.Remove(chart.Id);
		}

		private void HandleDeleteTRangeReferenceChart(TRangeReferenceChart chart)
		{
			TRangeReferenceChartRepository.DeleteTRangeReferenceChart(chart.Id);
			_savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
			_referenceChartBarsCache.Remove(chart.Id);
		}

		private List<CTraderBarRow>? GetChartBars(TRangeChart chart)
		{
			if (_chartBarsCache.TryGetValue(chart.Id, out var bars))
				return bars;
			if (_chartBarsLoading.Add(chart.Id))
				_ = LoadChartBarsAsync(chart);
			return null;
		}

		private async Task LoadChartBarsAsync(TRangeChart chart)
		{
			try
			{
				var config = new ProfilerConfig(
					chart.Symbol,
					chart.StartTimeUtc,
					chart.EndTimeUtc,
					TimeSpan.FromMinutes(chart.BarMinutes)
				);
				var loaded = await Service.LoadAsync(config, chart.EndTimeUtc, chart.BarCount);
				_chartBarsCache[chart.Id] = ChartBarRowHelper.ToBarRows(loaded.Bars);
			}
			finally
			{
				_chartBarsLoading.Remove(chart.Id);
			}
			StateHasChanged();
		}

		private List<CTraderBarRow>? GetReferenceChartBars(TRangeReferenceChart chart)
		{
			if (_referenceChartBarsCache.TryGetValue(chart.Id, out var bars))
				return bars;
			if (_referenceChartBarsLoading.Add(chart.Id))
				_ = LoadReferenceChartBarsAsync(chart);
			return null;
		}

		private async Task LoadReferenceChartBarsAsync(TRangeReferenceChart chart)
		{
			try
			{
				var config = new ProfilerConfig(
					chart.Symbol,
					chart.ReferenceStartTimeUtc,
					chart.EndTimeUtc,
					TimeSpan.FromMinutes(chart.BarMinutes)
				);
				var loaded = await Service.LoadAsync(
					config,
					chart.EndTimeUtc,
					chart.BarCount + chart.ReferenceBarCount
				);
				_referenceChartBarsCache[chart.Id] = ChartBarRowHelper.ToBarRows(loaded.Bars);
			}
			finally
			{
				_referenceChartBarsLoading.Remove(chart.Id);
			}
			StateHasChanged();
		}

		private List<ChartHorizontalLine> DrawingHorizontalLines() =>
			AllDrawings().SelectMany(d => d.HorizontalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

		private List<ChartVerticalLine> AllVerticalLines() =>
			AllDrawings()
				.SelectMany(d =>
					d.VerticalLines.Select(l => l with { ColorGroup = d.ColorGroup })
						.Concat(d.ReferenceLines.Select(l => l with { ColorGroup = "reference" }))
				)
				.ToList();

		private List<ChartDiagonalLine> AllDiagonalLines() =>
			AllDrawings().SelectMany(d => d.DiagonalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

		private List<ChartRangeFill> DrawingRangeFills() =>
			AllDrawings()
				.Where(d => d.IsRange)
				.Select(d => new ChartRangeFill(
					d.VerticalLines[0].TimeUtc,
					d.VerticalLines[1].TimeUtc,
					d.Id > 0 ? $"#{d.Id}" : null,
					d.ColorGroup
				))
				.Concat(
					AllDrawings()
						.Where(d => d.ReferenceLines.Count > 0 && d.VerticalLines.Count == 2)
						.Select(d =>
						{
							var earliest =
								d.VerticalLines[0].TimeUtc <= d.VerticalLines[1].TimeUtc
									? d.VerticalLines[0].TimeUtc
									: d.VerticalLines[1].TimeUtc;
							return new ChartRangeFill(
								d.ReferenceLines[0].TimeUtc,
								earliest,
								d.Id > 0 ? $"#{d.Id} REF" : null,
								"reference"
							);
						})
				)
				.ToList();
	}
}
