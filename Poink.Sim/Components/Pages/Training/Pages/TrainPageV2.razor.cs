using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Training.Components.TrainedModelsPanel;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
using Poink.Sim.Services.Symbols;
using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
	public partial class TrainPageV2 : ComponentBase, IDisposable
	{
		public void Dispose() => DetachFromRun();

		#region Tabs

		private enum TrainV2Tab
		{
			Data,
			Features,
			Network,
			Output,
			Training,
			Run,
			Live,
		}

		private static readonly TrainV2Tab[] Tabs =
		[
			TrainV2Tab.Data,
			TrainV2Tab.Features,
			TrainV2Tab.Network,
			TrainV2Tab.Output,
			TrainV2Tab.Training,
			TrainV2Tab.Run,
			TrainV2Tab.Live,
		];

		private TrainV2Tab _activeTab = TrainV2Tab.Data;

		// Background mode leaves Live with nothing current to show (see AttachToRun) -- hidden, not just disabled.
		private IEnumerable<TrainV2Tab> VisibleTabs => _backgroundMode ? Tabs.Where(t => t != TrainV2Tab.Live) : Tabs;

		private void SelectTab(TrainV2Tab tab)
		{
			_activeTab = tab;
			if (tab == TrainV2Tab.Training)
				_ = EnsurePreviewLoadedAsync();
		}

		private static string TabTitle(TrainV2Tab tab) =>
			tab switch
			{
				TrainV2Tab.Data => "What data is pulled, for which symbol and range",
				TrainV2Tab.Features => "Which encoders transform pulled data into the input vector",
				TrainV2Tab.Network => "The architecture that consumes the feature vector",
				TrainV2Tab.Output => "How the network's raw outputs map to trade actions",
				TrainV2Tab.Training => "How episodes are timed/sampled, and the trader each one starts with",
				TrainV2Tab.Run => "Start/pause/step the run and review live/saved results",
				TrainV2Tab.Live => "Watch the charts, equity curve and feature values for the episode currently in progress",				_ => "",
			};

		#endregion

		#region Data

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;

		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		// Same lookup TickDataSource itself uses -- MaxTime is null both when nothing's cached yet
		// and when _table matches nothing, so that's the "found?" check below.
		private (string Table, long Rows, DateTime? MinTime, DateTime? MaxTime) SelectedTableMeta =>
			TickRepository.CachedTables.FirstOrDefault(t => t.Table == _table);

		private long? _selectedRangeTickCount;

		private void CountSelectedRangeTicks() => _selectedRangeTickCount = TickRepository.CountTicks(_table, _from, _to);

		private static string FormatSpan(TimeSpan span) =>
			span.TotalDays >= 1 ? $"{span.TotalDays:0.#} days" : $"{span.TotalHours:0.#} hours";

		[Inject]
		private ISymbolMetadataCache SymbolMetadataCache { get; set; } = default!;

		[Inject]
		private ISymbolService SymbolService { get; set; } = default!;

		private SymbolModel? _selectedSymbol;
		private bool _loadingSymbolMetadata;

		private SymbolMetadataModel? SelectedSymbolMetadata =>
			_selectedSymbol is null ? null : SymbolMetadataCache.Get(_selectedSymbol.SymbolId);

		// SymbolDataSelector already resolved the symbol via the catalog -- this just fetches its
		// trading metadata (pip size, volume limits, etc.) for the Details card.
		private async Task OnSelectedSymbolChanged(SymbolModel? symbol)
		{
			_selectedSymbol = symbol;
			if (symbol is null)
				return;

			_loadingSymbolMetadata = true;
			try
			{
				await SymbolMetadataCache.EnsureAsync(symbol.SymbolId);
			}
			finally
			{
				_loadingSymbolMetadata = false;
			}
		}

		private DataOptionsModel CurrentDataOptions => new(_table, _from, _to, _selectedSymbol?.SymbolId, XGridPipSize);

		private void ApplyDataOptions(DataOptionsModel options)
		{
			_table = options.Table;
			_from = options.From;
			_to = options.To;
		}

		// SymbolDataSelector resolves Table back to a symbol on its own (ResolvePreselectedTable), but
		// doesn't notify the host -- restoring _selectedSymbol itself needs its own catalog lookup.
		private async Task ApplySelectedSymbolAsync(long? symbolId)
		{
			if (symbolId is null)
				return;

			var symbols = await SymbolService.ListAsync();
			var match = symbols.FirstOrDefault(s => s.SymbolId == symbolId);
			if (match is not null)
				await OnSelectedSymbolChanged(match);
		}

		#endregion

		#region Features

		// XGrid Signals -- mirrors BrainBuilder's FeaturesSection XGrid card, except Pip (derived from
		// the Data tab's selected symbol, not a free input) and Window (equal to Col, not separate).
		private HashSet<string> _xGridEnabledSignals = [];
		private double _xGridTopPercent = 10;
		private int _xGridColCount = 20;
		private int _xGridRowCount = 20;

		private double XGridPipSize => SelectedSymbolMetadata is { } metadata ? PositionPnlHelper.PipSize(metadata) : 0.0001;

		private bool AnyXGridSignalSelected => _xGridEnabledSignals.Count > 0;

		private int TotalXGridSignalCount => XGridSignalCatalog.All.Count;

		private int SelectedXGridSignalCount => _xGridEnabledSignals.Count;

		private bool AllXGridSignalsSelected => SelectedXGridSignalCount == TotalXGridSignalCount;

		private void ToggleAllXGridSignals(ChangeEventArgs e)
		{
			_xGridEnabledSignals =
				e.Value is true ? XGridSignalCatalog.All.Select(d => d.Key).ToHashSet() : [];
		}

		// Trader State -- deliberately minimal: unrealized/realized P/L as a percent of cash balance (zero both when flat and when flat P/L).
		private bool _traderStateIncludeUnrealized;
		private bool _traderStateIncludeRealized;
		private bool _traderStateIncludePreviousTradesRealized;
		private int _traderStatePreviousTradesCount = 5;

		private const int TotalTraderStateFieldCount = 3;

		private int SelectedTraderStateFieldCount =>
			(_traderStateIncludeUnrealized ? 1 : 0)
			+ (_traderStateIncludeRealized ? 1 : 0)
			+ (_traderStateIncludePreviousTradesRealized ? 1 : 0);

		// TxFold -- the real TxFold chart component owns its own toolbar/selection (bound here so the
		// Features grid below can read it); the grid itself shows field labels only, no fetched data.
		private HashSet<int> _txFoldSelectedSeconds = [60, 120, 240, 480, 900, 1800, 3600, 7200];
		private BarAggregationKind _txFoldBarKind = BarAggregationKind.Ohlc;
		private bool _txFoldIncludeHighLow;
		private bool _txFoldIncludeVolumeProfile;
		private int _txFoldVolumeProfileBucketCount = 20;
		private bool _txFoldIncludeCurrentPrice;
		private HashSet<TxFoldLevelKind> _txFoldIncludedLevels = [];
		private bool _txFoldNormalize = true;

		// Picked on the Features tab because it sets XGrid's window; the Training tab reads it for Episode span.
		private int _barWidthSeconds = 60;

		private const int TxFoldValuesPerTimeframe = 4;

		private int TxFoldValuesPerTimeframeSelected => TxFoldValuesPerTimeframe + (_txFoldIncludeHighLow ? 2 : 0);

		private int TxFoldFeatureCount =>
			_txFoldSelectedSeconds.Count * TxFoldValuesPerTimeframeSelected
			+ (_txFoldIncludeVolumeProfile ? _txFoldVolumeProfileBucketCount : 0)
			+ (_txFoldSelectedSeconds.Count > 0 && _txFoldIncludeCurrentPrice ? 1 : 0)
			+ (_txFoldSelectedSeconds.Count > 0 ? _txFoldIncludedLevels.Count : 0);

		private int TraderStateFeatureCount =>
			(_traderStateIncludeUnrealized ? 1 : 0)
			+ (_traderStateIncludeRealized ? 1 : 0)
			+ (_traderStateIncludePreviousTradesRealized ? _traderStatePreviousTradesCount : 0);

		private int TotalFeatureCount => SelectedXGridSignalCount + TxFoldFeatureCount + TraderStateFeatureCount;

		// XGrid's trailing window is one bar per column, so its span depends on the Training tab's bar width.
		private TimeSpan XGridWindow =>
			AnyXGridSignalSelected ? TimeSpan.FromSeconds((double)_xGridColCount * _barWidthSeconds) : TimeSpan.Zero;

		private TimeSpan TxFoldWindow => TimeSpan.FromSeconds(_txFoldSelectedSeconds.Max());

		// Trader State needs no history window -- account state is meaningful from the first tick.
		private IReadOnlyList<(string Source, TimeSpan Window)> FeatureWindows =>
			[("XGrid Signals", XGridWindow), ("TxFold", TxFoldWindow)];

		private (string Source, TimeSpan Window) LargestFeatureWindow => FeatureWindows.MaxBy(w => w.Window);

		private TimeSpan MinimumDataWindow => LargestFeatureWindow.Window;

		private static string FormatWindow(TimeSpan window)
		{
			if (window == TimeSpan.Zero)
				return "0";

			var parts = new List<string>();
			if (window.Days > 0)
				parts.Add($"{window.Days}d");
			if (window.Hours > 0)
				parts.Add($"{window.Hours}h");
			if (window.Minutes > 0)
				parts.Add($"{window.Minutes}m");
			if (window.Seconds > 0)
				parts.Add($"{window.Seconds}s");
			return string.Join(" ", parts);
		}

		private XGridSignalsOptionsModel CurrentXGridSignalsOptions =>
			new([.. _xGridEnabledSignals], _xGridTopPercent, _xGridColCount, _xGridRowCount);

		private TxFoldOptionsModel CurrentTxFoldOptions =>
			new(
				[.. _txFoldSelectedSeconds],
				_txFoldBarKind,
				_txFoldIncludeHighLow,
				_txFoldIncludeVolumeProfile,
				_txFoldVolumeProfileBucketCount,
				_txFoldIncludeCurrentPrice,
				[.. _txFoldIncludedLevels],
				_txFoldNormalize
			);

		private TraderStateOptionsModel CurrentTraderStateOptions =>
			new(_traderStateIncludeUnrealized, _traderStateIncludeRealized, _traderStateIncludePreviousTradesRealized, _traderStatePreviousTradesCount);

		private FeatureOptionsModel CurrentFeatureOptions => new(CurrentXGridSignalsOptions, CurrentTxFoldOptions, CurrentTraderStateOptions);

		private void ApplyFeatureOptions(FeatureOptionsModel options)
		{
			_xGridEnabledSignals = [.. options.XGridSignals.EnabledSignals];
			_xGridTopPercent = options.XGridSignals.TopPercent;
			_xGridColCount = options.XGridSignals.ColCount;
			_xGridRowCount = options.XGridSignals.RowCount;

			if (options.TxFold.SelectedSeconds is { } seconds)
				_txFoldSelectedSeconds = [.. seconds];
			_txFoldBarKind = options.TxFold.BarKind;
			_txFoldIncludeHighLow = options.TxFold.IncludeHighLow;
			_txFoldIncludeVolumeProfile = options.TxFold.IncludeVolumeProfile;
			_txFoldVolumeProfileBucketCount = options.TxFold.VolumeProfileBucketCount;
			_txFoldIncludeCurrentPrice = options.TxFold.IncludeCurrentPrice;
			if (options.TxFold.IncludedLevels is { } levels)
				_txFoldIncludedLevels = [.. levels];
			_txFoldNormalize = options.TxFold.Normalize;

			_traderStateIncludeUnrealized = options.TraderState.IncludeUnrealized;
			_traderStateIncludeRealized = options.TraderState.IncludeRealized;
			_traderStateIncludePreviousTradesRealized = options.TraderState.IncludePreviousTradesRealized;
			_traderStatePreviousTradesCount = options.TraderState.PreviousTradesCount;
		}

		#endregion

		#region Network

		// Structure -- mirrors BrainBuilder's StructSection, NEAT excluded entirely.
		private ArchitectureKind _architectureKind = ArchitectureKind.Mlp;
		private List<int> _mlpHiddenSizes = [64];
		private int _lstmHiddenSize = 64;
		private int _lstmNumLayers = 2;
		private int _cnnKernelSize = 3;
		private List<int> _cnnChannels = [16, 32];
		private CnnKernelSweepSource _cnnKernelSweepSource = CnnKernelSweepSource.BarWindow;

		private void AddMlpLayer() => _mlpHiddenSizes.Add(Math.Max(1, _mlpHiddenSizes[^1] / 2));

		private void AddMlpLayerOnTop() => _mlpHiddenSizes.Insert(0, _mlpHiddenSizes[0] * 2);

		private void RemoveMlpLayer(int index)
		{
			if (_mlpHiddenSizes.Count > 1)
				_mlpHiddenSizes.RemoveAt(index);
		}

		private void OnMlpLayerSizeChanged(int index, ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), out var size))
				_mlpHiddenSizes[index] = size;
		}

		private void AddCnnChannelLayer() => _cnnChannels.Add(_cnnChannels[^1] * 2);

		private void RemoveCnnChannelLayer(int index)
		{
			if (_cnnChannels.Count > 1)
				_cnnChannels.RemoveAt(index);
		}

		private void OnCnnChannelSizeChanged(int index, ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), out var size))
				_cnnChannels[index] = size;
		}

		// One column per hidden layer, one stacked square per unit (capped visually) --
		// deliberately its own visual, not TrainingPreview's per-layer bar chart.
		private const int MaxVisibleUnits = 7;

		private sealed record NetworkColumn(string Label, int Size, int VisibleUnits)
		{
			public int HiddenUnits => Size - VisibleUnits;
		}

		private List<NetworkColumn> NetworkColumns
		{
			get
			{
				var layers = _architectureKind switch
				{
					ArchitectureKind.Lstm => Enumerable.Range(0, _lstmNumLayers)
						.Select(i => ($"L{i + 1}", _lstmHiddenSize))
						.ToList(),
					ArchitectureKind.Cnn => _cnnChannels.Select((c, i) => ($"C{i + 1}", c)).ToList(),
					_ => _mlpHiddenSizes.Select((s, i) => ($"L{i + 1}", s)).ToList(),
				};

				return layers
					.Where(layer => layer.Item2 > 0)
					.Select(layer => new NetworkColumn(layer.Item1, layer.Item2, VisibleUnitCount(layer.Item2)))
					.ToList();
			}
		}

		// A "+N" label takes the last visible slot when the layer is too big to draw in full.
		private static int VisibleUnitCount(int size) => size > MaxVisibleUnits ? MaxVisibleUnits - 1 : size;

		private NetworkOptionsModel CurrentNetworkOptions =>
			new(_architectureKind, [.. _mlpHiddenSizes], _lstmHiddenSize, _lstmNumLayers, _cnnKernelSize, [.. _cnnChannels], _cnnKernelSweepSource);

		private void ApplyNetworkOptions(NetworkOptionsModel options)
		{
			_architectureKind = options.ArchitectureKind;
			_mlpHiddenSizes = options.MlpHiddenSizes is { } mlp ? [.. mlp] : [64];
			_lstmHiddenSize = options.LstmHiddenSize;
			_lstmNumLayers = options.LstmNumLayers;
			_cnnKernelSize = options.CnnKernelSize;
			_cnnChannels = options.CnnChannels is { } cnn ? [.. cnn] : [16, 32];
			_cnnKernelSweepSource = options.CnnKernelSweepSource;
		}

		#endregion

		#region Output

		// Actions -- mirrors BrainBuilder's ActionsSection Actions card.
		private bool _includeHold = true;
		private bool _includeEnterLong = true;
		private bool _includeEnterShort = true;
		private bool _includeClose = true;

		private const int TotalActionOptionCount = 4;

		private bool AllActionsSelected => DirectionCount == TotalActionOptionCount;

		private void ToggleAllActions(ChangeEventArgs e)
		{
			var value = e.Value is true;
			_includeHold = value;
			_includeEnterLong = value;
			_includeEnterShort = value;
			_includeClose = value;
		}

		private IReadOnlyList<string> EnabledDirectionLabels
		{
			get
			{
				var labels = new List<string>();
				if (_includeHold)
					labels.Add("Hold");
				if (_includeEnterLong)
					labels.Add("Enter Long");
				if (_includeEnterShort)
					labels.Add("Enter Short");
				if (_includeClose)
					labels.Add("Close");
				return labels;
			}
		}

		private int DirectionCount => EnabledDirectionLabels.Count;

		private int TotalOutputWidth => DirectionCount;

		// Action Masking -- mirrors BrainBuilder's ActionsSection Action Masking card, defaulting every
		// rule on. The XGrid-dependent rules are left out for now.
		private bool _maskPositionRequiredForClose = true;
		private bool _maskFlatRequiredForEntry = true;

		private sealed record MaskRuleRow(string Label, string Tooltip, Func<bool> Get, Action<bool> Set);

		private IReadOnlyList<MaskRuleRow> MaskRuleRows =>
			new List<MaskRuleRow>
			{
				new(
					"Close Requires Position",
					"Close is illegal unless a position is currently open",
					() => _maskPositionRequiredForClose,
					v => _maskPositionRequiredForClose = v
				),
				new(
					"Entry Requires Flat",
					"Enter Long / Enter Short are illegal while a position is already open",
					() => _maskFlatRequiredForEntry,
					v => _maskFlatRequiredForEntry = v
				),
			};

		private void OnMaskRuleFieldChanged(MaskRuleRow row, ChangeEventArgs e) => row.Set(e.Value is true);

		private const int TotalMaskRuleCount = 2;

		private int SelectedMaskRuleCount => MaskRuleRows.Count(r => r.Get());

		private bool AllMaskRulesSelected => SelectedMaskRuleCount == TotalMaskRuleCount;

		private void ToggleAllMaskRules(ChangeEventArgs e)
		{
			var value = e.Value is true;
			foreach (var row in MaskRuleRows)
				row.Set(value);
		}

		// Action Decoding -- mirrors BrainBuilder's ActionsSection Action Decoding card.
		private DecisionDecodingKind _decisionDecoding = DecisionDecodingKind.Softmax;
		private double _decisionTemperature = 1.0;

		private string DecodingLabel =>
			_decisionDecoding switch
			{
				DecisionDecodingKind.HardMax => "hardmax",
				_ when _decisionTemperature != 1.0 => $"softmax @ T={_decisionTemperature:0.##}",
				_ => "softmax",
			};

		private ActionsOptionsModel CurrentActionsOptions =>
			new(_includeHold, _includeEnterLong, _includeEnterShort, _includeClose);

		private ActionMaskingOptionsModel CurrentActionMaskingOptions =>
			new(_maskPositionRequiredForClose, _maskFlatRequiredForEntry);

		private ActionDecodingOptionsModel CurrentActionDecodingOptions => new(_decisionDecoding, _decisionTemperature);

		private OutputOptionsModel CurrentOutputOptions => new(CurrentActionsOptions, CurrentActionMaskingOptions, CurrentActionDecodingOptions);

		private void ApplyOutputOptions(OutputOptionsModel options)
		{
			_includeHold = options.Actions.IncludeHold;
			_includeEnterLong = options.Actions.IncludeEnterLong;
			_includeEnterShort = options.Actions.IncludeEnterShort;
			_includeClose = options.Actions.IncludeClose;

			_maskPositionRequiredForClose = options.ActionMasking.PositionRequiredForClose;
			_maskFlatRequiredForEntry = options.ActionMasking.FlatRequiredForEntry;

			_decisionDecoding = options.ActionDecoding.DecisionDecoding;
			_decisionTemperature = options.ActionDecoding.DecisionTemperature;
		}

		#endregion

		#region Training

		// Episode Timing -- mirrors TrainPage's Episode Timing panel.
		private int _episodeCount = 1000;
		private int _trainingBars = 20;
		private int _visibleBars = 120;

		// Off by default, so an episode may start anywhere in the Data tab's range. Weekday and
		// session are independent, combinable filters.
		private bool _restrictEpisodeTiming;
		private readonly HashSet<DayOfWeek> _allowedWeekdays = [.. Enum.GetValues<DayOfWeek>()];
		private TimeSpan _sessionStart = TimeSpan.Zero;
		private TimeSpan _sessionEnd = TimeSpan.FromHours(24);

		private static readonly DayOfWeek[] WeekdayDisplayOrder =
		[
			DayOfWeek.Monday,
			DayOfWeek.Tuesday,
			DayOfWeek.Wednesday,
			DayOfWeek.Thursday,
			DayOfWeek.Friday,
			DayOfWeek.Saturday,
			DayOfWeek.Sunday,
		];

		// When restricted, Training bars derives from session length / bar width instead of being picked directly.
		private int EffectiveTrainingBars =>
			_restrictEpisodeTiming ? (int)((_sessionEnd - _sessionStart).TotalSeconds / _barWidthSeconds) : _trainingBars;

		private int EpisodeBars => _visibleBars + EffectiveTrainingBars;

		private TimeSpan VisibleWindow => TimeSpan.FromSeconds((double)_visibleBars * _barWidthSeconds);

		private int MinimumVisibleBars => (int)Math.Ceiling(MinimumDataWindow.TotalSeconds / _barWidthSeconds);

		private bool VisibleBarsCoverFeatures => _visibleBars >= MinimumVisibleBars;

		private TimeSpan EpisodeSpan => TimeSpan.FromSeconds((double)EpisodeBars * _barWidthSeconds);

		// Episode Timing preview -- a read-only look at the Data tab's selected range, reusing
		// ProfilerPage's chunked backfill so panning the chart loads older history on demand.
		[Inject]
		private IProfilerService ProfilerService { get; set; } = default!;

		private const int PreviewBarsPerFetch = 300;

		private ProfilerConfig? _previewConfig;
		private List<HlocAvdtBar> _previewBars = new();
		private List<CTraderBarRow> _previewBarRows = new();
		private double? LatestPreviewPrice => _previewBars.Count > 0 ? _previewBars[^1].Close : null;
		private bool _previewLoading;
		private bool _previewLoadingMore;
		private bool _previewReachedStart;
		private bool _previewKeepView;
		private string? _previewError;

		// Consumed by DataProvider when SamplingMode is WindowSelected -- see CurrentEpisodeTimingOptions.
		private DateTime? _previewWindowStart;
		private DateTime? _previewWindowEnd;

		private sealed record EpisodeSamplingModeOption(EpisodeSamplingMode Mode, string Label, string Tooltip);

		private static readonly EpisodeSamplingModeOption[] EpisodeSamplingModeOptions =
		[
			new(EpisodeSamplingMode.RandomFromSet, "Random from set",
				"Each episode starts at a random point within the Data tab's selected range, sized/positioned by the fields below"),
			new(EpisodeSamplingMode.EntireSet, "Entire set",
				"Every episode trains over the Data tab's whole selected range, start to finish, after preloading the feature window before it"),
			new(EpisodeSamplingMode.WindowSelected, "Window selected",
				"Every episode trains over the exact range marked with the LOOKBACK WINDOW tool on the chart below, after preloading the feature window before it"),
		];

		private EpisodeSamplingMode _episodeSamplingMode = EpisodeSamplingMode.RandomFromSet;

		private List<ChartRangeFill> PreviewRangeFills() =>
			_previewWindowStart is { } start && _previewWindowEnd is { } end
				? [new ChartRangeFill(start - VisibleWindow, start, "FEATURE WINDOW", "reference"), new ChartRangeFill(start, end, "TRAINING WINDOW", "volume")]
				: [];

		// The marked window itself is the episode's training set -- Visible bars is still a separate
		// lead-in DataProvider fetches before it, same as RandomFromSet.
		private int WindowTrainingBarCount =>
			_previewWindowStart is { } start && _previewWindowEnd is { } end
				? _previewBars.Count(b => b.OpenTime >= start && b.OpenTime <= end)
				: 0;

		private int WindowEpisodeBars => WindowTrainingBarCount + _visibleBars;

		private (double Min, double Max)? WindowPriceRange
		{
			get
			{
				if (_previewWindowStart is not { } start || _previewWindowEnd is not { } end)
					return null;

				var barsInWindow = _previewBars.Where(b => b.OpenTime >= start && b.OpenTime <= end).ToList();
				return barsInWindow.Count == 0 ? null : (barsInWindow.Min(b => b.Low), barsInWindow.Max(b => b.High));
			}
		}

		private static string FormatTimestamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

		private async Task EnsurePreviewLoadedAsync()
		{
			if (string.IsNullOrWhiteSpace(_table))
				return;

			var config = new ProfilerConfig(_table, _from, _to, TimeSpan.FromSeconds(_barWidthSeconds));
			if (_previewConfig == config)
				return;

			_previewConfig = config;
			_previewBars = new();
			_previewBarRows = new();
			_previewReachedStart = false;
			_previewKeepView = false;
			_previewError = null;
			_previewLoading = true;

			try
			{
				var chunk = await ProfilerService.LoadAsync(config, config.To, PreviewBarsPerFetch);
				ApplyPreviewChunk(chunk);
			}
			catch (Exception ex)
			{
				_previewError = ex.Message;
			}
			finally
			{
				_previewLoading = false;
			}

			// Skip if the user already navigated away -- re-rendering another tab here races its own lifecycle.
			if (_activeTab == TrainV2Tab.Training)
				StateHasChanged();
		}

		private async Task LoadMorePreview()
		{
			_previewKeepView = true;
			if (_previewConfig is null || _previewReachedStart || _previewLoadingMore)
				return;

			_previewLoadingMore = true;
			try
			{
				var cursor = _previewBars.Count > 0 ? _previewBars[0].OpenTime : _previewConfig.To;
				var chunk = await ProfilerService.LoadAsync(_previewConfig, cursor, PreviewBarsPerFetch);
				ApplyPreviewChunk(chunk);
			}
			catch (Exception ex)
			{
				_previewError = ex.Message;
			}
			finally
			{
				_previewLoadingMore = false;
			}
		}

		private void ApplyPreviewChunk(ProfilerChunk chunk)
		{
			_previewBars = chunk.Bars.Concat(_previewBars).ToList();
			_previewBarRows = ToBarRows(_previewBars);
			_previewReachedStart = chunk.ReachedStart;
		}

		private static List<CTraderBarRow> ToBarRows(IEnumerable<HlocAvdtBar> bars) =>
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

		private void SetPreviewWindow(ChartTimeRange range)
		{
			_previewWindowStart = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
			_previewWindowEnd = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
		}

		private void ClearPreviewWindow()
		{
			_previewWindowStart = null;
			_previewWindowEnd = null;
		}

		private void ToggleRestrictEpisodeTiming(ChangeEventArgs e) => _restrictEpisodeTiming = e.Value is true;

		private void ToggleWeekday(DayOfWeek day)
		{
			if (!_allowedWeekdays.Remove(day))
				_allowedWeekdays.Add(day);
		}

		private static string WeekdayAbbreviation(DayOfWeek day) =>
			day switch
			{
				DayOfWeek.Monday => "Mo",
				DayOfWeek.Tuesday => "Tu",
				DayOfWeek.Wednesday => "We",
				DayOfWeek.Thursday => "Th",
				DayOfWeek.Friday => "Fr",
				DayOfWeek.Saturday => "Sa",
				DayOfWeek.Sunday => "Su",
				_ => "",
			};

		private string SessionStartText
		{
			get => EpisodeTimeConstraint.FormatClock(_sessionStart);
			set
			{
				if (TimeSpan.TryParse(value, out var parsed))
					_sessionStart = parsed;
			}
		}

		private string SessionEndText
		{
			get => EpisodeTimeConstraint.FormatClock(_sessionEnd);
			set
			{
				if (value.Trim() is "24:00" or "24:00:00" or "24")
					_sessionEnd = TimeSpan.FromHours(24);
				else if (TimeSpan.TryParse(value, out var parsed))
					_sessionEnd = parsed;
			}
		}

		// Trader Setup -- mirrors TrainPage's Trader panel.
		private double _startingCash = 10_000;
		private double _leverage = 1;
		private double _commissionRatePercent;
		private double _stopOutLevelPercent = 50;

		private PositionControlOptionsModel _positionControl = new();

		// Learning -- mirrors BrainBuilder's LearnSection, NEAT excluded from the strategy options.
		private static readonly IReadOnlyList<string> TrainingStrategyOptions = TrainingStrategyCatalog.Names;

		private string _trainingStrategyName = TrainingStrategyOptions[0];
		private double _learningRate = 0.001;
		private bool _useEntropyBonus = true;

		// Decision Masking gates whether the network is even asked to decide, as opposed to
		// Action Masking (Output tab) which restricts choices afterward.
		private sealed record DecisionMaskResolutionOption(DecisionMaskResolutionKind Kind, string Label, string Tooltip);

		private static readonly DecisionMaskResolutionOption[] DecisionMaskResolutionOptions =
		[
			new(DecisionMaskResolutionKind.PerBar, "Once per bar", "The network decides once, when each bar closes"),
			new(DecisionMaskResolutionKind.EverySeconds, "Every X seconds", "The network decides on a fixed time interval"),
			new(DecisionMaskResolutionKind.PerTick, "Every tick", "The network decides on every incoming tick"),
		];

		private DecisionMaskResolutionKind _decisionMaskResolution = DecisionMaskResolutionKind.PerBar;
		private double _decisionMaskResolutionSeconds = 1.0;

		private string DecisionMaskResolutionLabel =>
			_decisionMaskResolution switch
			{
				DecisionMaskResolutionKind.EverySeconds => $"every {_decisionMaskResolutionSeconds:0.###}s",
				DecisionMaskResolutionKind.PerTick => "every tick",
				_ => "once per bar",
			};

		private EpisodeTimingOptionsModel CurrentEpisodeTimingOptions =>
			new(
				_episodeCount,
				_trainingBars,
				_visibleBars,
				_restrictEpisodeTiming,
				[.. _allowedWeekdays],
				_sessionStart,
				_sessionEnd,
				_episodeSamplingMode,
				_previewWindowStart,
				_previewWindowEnd
			);

		private TraderSetupOptionsModel CurrentTraderSetupOptions =>
			new(_startingCash, _leverage, _commissionRatePercent, _stopOutLevelPercent, _positionControl);

		private LearningOptionsModel CurrentLearningOptions => new(_trainingStrategyName, _learningRate, _useEntropyBonus);

		private DecisionMaskingOptionsModel CurrentDecisionMaskingOptions => new(_decisionMaskResolution, _decisionMaskResolutionSeconds);

		private TrainingTabOptionsModel CurrentTrainingTabOptions =>
			new(CurrentEpisodeTimingOptions, CurrentTraderSetupOptions, CurrentLearningOptions, CurrentDecisionMaskingOptions);

		private void ApplyTrainingTabOptions(TrainingTabOptionsModel options)
		{
			var episodeTiming = options.EpisodeTiming;
			_episodeCount = episodeTiming.EpisodeCount;
			_trainingBars = episodeTiming.TrainingBars;
			_visibleBars = episodeTiming.VisibleBars;
			_restrictEpisodeTiming = episodeTiming.RestrictEpisodeTiming;
			_allowedWeekdays.Clear();
			foreach (var day in episodeTiming.AllowedWeekdays ?? Enum.GetValues<DayOfWeek>())
				_allowedWeekdays.Add(day);
			_sessionStart = episodeTiming.SessionStart;
			_sessionEnd = episodeTiming.SessionEnd ?? TimeSpan.FromHours(24);
			_episodeSamplingMode = episodeTiming.SamplingMode;
			_previewWindowStart = episodeTiming.WindowStart;
			_previewWindowEnd = episodeTiming.WindowEnd;

			var traderSetup = options.TraderSetup;
			_startingCash = traderSetup.StartingCash;
			_leverage = traderSetup.Leverage;
			_commissionRatePercent = traderSetup.CommissionRatePercent;
			_stopOutLevelPercent = traderSetup.StopOutLevelPercent;
			_positionControl = traderSetup.EffectiveControl;

			var learning = options.Learning;
			_trainingStrategyName = TrainingStrategyCatalog.Normalize(learning.TrainingStrategyName);
			_learningRate = learning.LearningRate;
			_useEntropyBonus = learning.UseEntropyBonus;

			var decisionMasking = options.DecisionMasking;
			_decisionMaskResolution = DecisionMaskResolutionOptions.Any(o => o.Kind == decisionMasking.DecisionMaskResolution)
				? decisionMasking.DecisionMaskResolution
				: DecisionMaskResolutionKind.PerBar;
			_decisionMaskResolutionSeconds = decisionMasking.DecisionMaskResolutionSeconds;
		}

		#endregion

		#region Run

		[Inject]
		private TrainingRunService TrainingRun { get; set; } = default!;

		private bool _optionsSummaryCollapsed;

		// Compact architecture-shape label for the Options Summary card -- NetworkColumns is the
		// visual (squares) version of this same data.
		private string NetworkSummaryText =>
			_architectureKind switch
			{
				ArchitectureKind.Lstm => $"{_lstmNumLayers} x {_lstmHiddenSize} (LSTM)",
				ArchitectureKind.Cnn => $"{string.Join(" -> ", _cnnChannels)} (CNN, kernel {_cnnKernelSize})",
				_ => string.Join(" -> ", _mlpHiddenSizes),
			};

		// Forces max speed and skips the live callbacks entirely -- see AttachToRun.
		private bool _backgroundMode;

		private void ToggleBackgroundMode()
		{
			_backgroundMode = !_backgroundMode;
			if (_backgroundMode && _activeTab == TrainV2Tab.Live)
				_activeTab = TrainV2Tab.Run;
		}

		private TrainingRunObserver? _runObserver;

		private int TargetEpisodeCount =>
			TrainingRun.TargetEpisodeCount > 0 ? TrainingRun.TargetEpisodeCount : CurrentTrainingOptions.Training.EpisodeTiming.EpisodeCount;

		private double RunProgressPercent =>
			TargetEpisodeCount == 0 ? 0 : Math.Min(100.0, TrainingRun.EpisodesCompleted * 100.0 / TargetEpisodeCount);

		private double WinRatePercent => TrainingRun.EpisodeMetrics.Count == 0 ? 0 : RlEpisodeMetricStats.WinRate(TrainingRun.EpisodeMetrics);

		private double MedianReward => TrainingRun.EpisodeMetrics.Count == 0 ? 0 : RlEpisodeMetricStats.Median(TrainingRun.EpisodeMetrics);

		private double RewardStdDev => TrainingRun.EpisodeMetrics.Count == 0 ? 0 : RlEpisodeMetricStats.StdDev(TrainingRun.EpisodeMetrics);

		private double RewardSpread => TrainingRun.EpisodeMetrics.Count == 0 ? 0 : TrainingRun.BestReward - TrainingRun.WorstReward;

		// The run outlives this page, so the page only watches it -- detaching drops the run to headless max speed.
		private void AttachToRun()
		{
			_runObserver = new TrainingRunObserver(
				() => !_backgroundMode,
				ResetLiveEpisodeState,
				OnLiveDecision,
				OnLiveTick,
				() => _backgroundMode ? TimeSpan.Zero : LiveTickDelay,
				() => _activeTab == TrainV2Tab.Live && _liveShowActions
			);
			TrainingRun.Attach(_runObserver);
			TrainingRun.Changed += HandleRunChanged;
		}

		private void DetachFromRun()
		{
			TrainingRun.Changed -= HandleRunChanged;
			if (_runObserver is not null)
				TrainingRun.Detach(_runObserver);
		}

		private void HandleRunChanged() => _ = InvokeAsync(StateHasChanged);

		private void StartRun() => TrainingRun.Start(CurrentTrainingOptions);

		private Task StepRun() => TrainingRun.Step(CurrentTrainingOptions);

		private void ResetRun()
		{
			TrainingRun.Reset();
			ResetLiveEpisodeState();
		}

		private void PauseRun() => TrainingRun.Pause();

		private void ResumeRun() => TrainingRun.Resume();

		private void StopRun() => TrainingRun.Stop();

		#endregion

		#region Live

		private const int MaxLiveBars = 500;
		private const int MaxLiveEquityPoints = 2000;

		// One atomically-swapped, fully-immutable snapshot, not several separately-mutated fields --
		// OnLiveDecision runs on the background training thread while the page renders on its own.
		private sealed record LiveUiState(
			LiveEpisodeSnapshot Snapshot,
			IReadOnlyList<CTraderBarRow> Bars,
			IReadOnlyList<DateTime> EquityTimestamps,
			IReadOnlyList<double> EquityValues,
			int ActionCount
		);

		private LiveUiState? _liveState;

		// Owned here (not as LiveEpisodeView's own local state) so the panel selection survives
		// switching away from the Live tab and back -- that recreates LiveEpisodeView from scratch.
		private bool _liveShowMarketChart = true;
		private bool _liveShowXGrid;
		private bool _liveShowTxFold = true;
		private bool _liveShowEquityCurve = true;
		private bool _liveShowFeatures = true;
		private bool _liveShowActions = true;

		private void ResetLiveEpisodeState() => _liveState = null;

		// RunEpisode's onDecision callback -- fires on the background training-loop thread, once per decision.
		private void OnLiveDecision(LiveEpisodeSnapshot snapshot)
		{
			var previous = _liveState;

			var bars = previous?.Bars ?? snapshot.HistoryBars?.TakeLast(MaxLiveBars).ToList() ?? [];
			if (snapshot.ClosedBar is { } closed)
			{
				var updatedBars = new List<CTraderBarRow>(bars) { closed };
				if (updatedBars.Count > MaxLiveBars)
					updatedBars.RemoveAt(0);
				bars = updatedBars;
			}

			var timestamps = new List<DateTime>(previous?.EquityTimestamps ?? []) { snapshot.Timestamp };
			var values = new List<double>(previous?.EquityValues ?? []) { snapshot.Capital.Equity };
			if (timestamps.Count > MaxLiveEquityPoints)
			{
				timestamps.RemoveAt(0);
				values.RemoveAt(0);
			}

			_liveState = new LiveUiState(snapshot, bars, timestamps, values, (previous?.ActionCount ?? 0) + 1);

			_ = InvokeAsync(StateHasChanged);
		}

		private static readonly TimeSpan LiveRenderInterval = TimeSpan.FromMilliseconds(33);

		private long _lastLiveRenderTicks;

		// RunEpisode's onTick callback -- folds the latest tick into the last decision's snapshot so the
		// status row, position cards and chart move every tick; renders are throttled, state is not.
		private void OnLiveTick(LiveTickSnapshot tick)
		{
			if (_liveState is not { } previous)
				return;

			var bars = previous.Bars;
			if (tick.ClosedBar is { } closed)
			{
				var updatedBars = new List<CTraderBarRow>(bars) { closed };
				if (updatedBars.Count > MaxLiveBars)
					updatedBars.RemoveAt(0);
				bars = updatedBars;
			}

			var snapshot = previous.Snapshot with
			{
				Timestamp = tick.Timestamp,
				Bid = tick.Bid,
				Ask = tick.Ask,
				CurrentBar = tick.CurrentBar,
				Capital = tick.Capital,
				OpenPosition = tick.OpenPosition,
				ClosedTrades = tick.ClosedTrades ?? previous.Snapshot.ClosedTrades,
				RawDirectionOutputs = tick.RawDirectionOutputs ?? previous.Snapshot.RawDirectionOutputs,
				PreMaskDirectionOutputs = tick.PreMaskDirectionOutputs ?? previous.Snapshot.PreMaskDirectionOutputs,
				TicksProcessed = tick.TicksProcessed,
				TicksTotal = tick.TicksTotal,
			};
			_liveState = previous with { Snapshot = snapshot, Bars = bars };

			var now = Stopwatch.GetTimestamp();
			if (Stopwatch.GetElapsedTime(_lastLiveRenderTicks, now) < LiveRenderInterval)
				return;
			_lastLiveRenderTicks = now;
			_ = InvokeAsync(StateHasChanged);
		}

		// Includes the in-progress bar so the chart's last candle keeps updating between closes.
		private static List<CTraderBarRow> BuildLiveChartBars(LiveUiState? live) =>
			live is { } s ? [.. s.Bars, s.Snapshot.CurrentBar] : [];

		private static IReadOnlyList<EquityCurveSample> BuildLiveEquitySamples(LiveUiState? live) =>
			live is null ? [] : EquityCurveHelper.Build(live.EquityTimestamps, live.EquityValues, live.Snapshot.ClosedTrades);

		private IReadOnlyList<FeatureDisplayRow> BuildLiveFeatureRows(LiveUiState? live) =>
			live is { } s ? FeatureDisplayFormatter.Build(s.Snapshot.Features, _txFoldBarKind) : [];

		private IReadOnlyList<TxFoldGridRow> BuildLiveTxFoldRows(LiveUiState? live) =>
			live is { } s ? FeatureDisplayFormatter.BuildTxFoldGrid(s.Snapshot.Features, _txFoldBarKind) : [];

		private static double BuildLiveEpisodeProgress(LiveUiState? live) =>
			live is { Snapshot.TicksTotal: > 0 } s ? (double)s.Snapshot.TicksProcessed / s.Snapshot.TicksTotal : 0;

		private static TimeSpan? BuildLiveTimePassed(LiveUiState? live) =>
			live is { } s ? s.Snapshot.Timestamp - s.Snapshot.EpisodeStart : null;

		// Direction is the only group the engine decides today (Volume/StopLoss/TakeProfit throw if
		// enabled) -- feeds BrainIoPanel, the same action-distribution card the old training page uses.
		private IReadOnlyList<RlOutputFeature>? BuildLiveOutputFeatures(LiveUiState? live)
		{
			if (live is null || TrainingRun.ActionSpace is not { } actionSpace)
				return null;

			var s = live.Snapshot;
			var probabilities = actionSpace.Probabilities(s.RawDirectionOutputs);
			var preMask = s.PreMaskDirectionOutputs ?? s.RawDirectionOutputs;
			var preMaskProbabilities = actionSpace.Probabilities(preMask);
			return actionSpace
				.Directions.Select(
					(direction, i) => new RlOutputFeature(
						direction.ToString(),
						probabilities[i],
						direction == s.Action,
						s.RawDirectionOutputs[i],
						PreMaskProbability: preMaskProbabilities[i],
						PreMaskLogit: preMask[i]
					)
				)
				.ToList();
		}

		// Speed governs the actual training loop's pace (RunEpisode's tickDelay, applied per tick), not just redraws.
		// A null step is MAX -- tickDelay becomes exactly zero, real unthrottled training speed.
		private static readonly double?[] LiveSpeedOptions = { 0.1, 1, 10, 100, 1000, null };
		private const double LiveBaseTickDelayMs = 20;

		private int _liveSpeedIndex = 1;

		private TimeSpan LiveTickDelay =>
			LiveSpeedOptions[_liveSpeedIndex] is { } multiplier ? TimeSpan.FromMilliseconds(LiveBaseTickDelayMs / multiplier) : TimeSpan.Zero;

		private static string LiveSpeedLabel(double? multiplier) => multiplier is { } value ? FormatSpeedMultiplier(value) : "MAX";

		private static string FormatSpeedMultiplier(double speed) =>
			(speed < 1 ? speed.ToString("0.##", CultureInfo.InvariantCulture) : speed.ToString("0", CultureInfo.InvariantCulture)) + "x";

		private void DecreaseLiveSpeed() => _liveSpeedIndex = Math.Max(0, _liveSpeedIndex - 1);

		private void IncreaseLiveSpeed() => _liveSpeedIndex = Math.Min(LiveSpeedOptions.Length - 1, _liveSpeedIndex + 1);

		private void MaxLiveSpeed() => _liveSpeedIndex = LiveSpeedOptions.Length - 1;

		// Single Play/Pause toggle for the Live tab -- Start/Pause/Resume stay separate methods
		// since the Run tab still shows them as distinct buttons.
		private void ToggleLiveRun()
		{
			if (TrainingRun.IsPaused)
				ResumeRun();
			else if (TrainingRun.IsRunning)
				PauseRun();
			else
				StartRun();
		}

		#endregion

		#region Models

		[Inject]
		private TrainedModelService TrainedModelService { get; set; } = default!;

		[Inject]
		private TrainingHistoryService TrainingHistoryService { get; set; } = default!;

		[Inject]
		private ILogger<TrainPageV2> Logger { get; set; } = default!;

		private bool _modelsPanelCollapsed;
		private ModelsPanelTab _modelsPanelTab = ModelsPanelTab.Models;

		protected override Task OnInitializedAsync()
		{
			AttachToRun();
			return RefreshModelsWithStatusAsync();
		}

		private Task RefreshModelsWithStatusAsync() =>
			ReportModelsAsync(async () =>
			{
				await RefreshModelsAsync();
				return null;
			});

		private async Task ToggleModelsPanel()
		{
			_modelsPanelCollapsed = !_modelsPanelCollapsed;
			if (!_modelsPanelCollapsed)
				await RefreshModelsWithStatusAsync();
		}

		private async Task SelectModelsPanelTabAsync(ModelsPanelTab tab)
		{
			_modelsPanelTab = tab;
			if (!_modelsPanelCollapsed)
				return;

			_modelsPanelCollapsed = false;
			await RefreshModelsWithStatusAsync();
		}

		private static string ModelsPanelTabTitle(ModelsPanelTab tab) =>
			tab switch
			{
				ModelsPanelTab.Models => "Saved models: save the current one, load or delete others -- click a card to filter History to that model",
				ModelsPanelTab.History => "Saved training results, for the selected model or for all models",
				_ => "",
			};

		private IReadOnlyList<TrainedModelSummary> _savedModels = [];
		private IReadOnlyList<TrainingHistorySummary> _modelHistory = [];
		private Guid? _selectedModelId;
		private string? _modelsStatus;
		private bool _modelsStatusIsError;

		private bool IsTrainingInProgress => TrainingRun.IsTrainingInProgress;

		private bool CanSaveModel => TrainingRun.Network is not null && !IsTrainingInProgress;

		private string? SaveModelBlockedReason =>
			TrainingRun.Network is null ? "Nothing to save yet -- start or step the run once so a network exists"
			: IsTrainingInProgress ? "Pause or stop the run first -- the weights are still changing"
			: null;

		private bool CanSaveResults =>
			_selectedModelId is not null && TrainingRun.EpisodeMetrics.Count > 0 && !TrainingRun.IsViewingSavedResults && !IsTrainingInProgress;

		private string? SaveResultsBlockedReason =>
			_selectedModelId is null ? "Click a model's card first -- results are saved under the selected model"
			: TrainingRun.IsViewingSavedResults ? "These are already saved results -- start a run to produce new ones"
			: TrainingRun.EpisodeMetrics.Count == 0 ? "No episodes have completed yet"
			: IsTrainingInProgress ? "Pause or stop the run first"
			: null;

		private bool CanLoadModels => !TrainingRun.IsRunning;

		private string? LoadModelsBlockedReason => TrainingRun.IsRunning ? "Stop the run first" : null;

		private async Task ReportModelsAsync(Func<Task<string?>> action)
		{
			try
			{
				_modelsStatus = await action();
				_modelsStatusIsError = false;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Models panel action failed");
				_modelsStatus = ex.Message;
				_modelsStatusIsError = true;
			}

			StateHasChanged();
		}

		private async Task RefreshModelsAsync()
		{
			_savedModels = await TrainedModelService.ListAsync();
			if (_selectedModelId is { } selected && _savedModels.All(m => m.Id != selected))
				_selectedModelId = null;
			await RefreshHistoryAsync();
		}

		private async Task RefreshHistoryAsync() => _modelHistory = await TrainingHistoryService.ListAsync(_selectedModelId);

		private Task DeselectModelAsync() =>
			ReportModelsAsync(async () =>
			{
				_selectedModelId = null;
				await RefreshHistoryAsync();
				return null;
			});

		private Task SaveModelAsync(string name) =>
			ReportModelsAsync(async () =>
			{
				var id = await TrainedModelService.SaveAsync(
					name,
					CurrentTrainingOptions,
					TrainingRun.Network!,
					TrainingRun.LoadedEpisodesTrained + TrainingRun.EpisodesCompleted
				);
				_selectedModelId = id;
				await RefreshModelsAsync();
				return $"Saved model '{name.Trim()}'.";
			});

		private Task LoadModelAsync(Guid id) =>
			ReportModelsAsync(async () =>
			{
				var loaded = await TrainedModelService.LoadAsync(id) ?? throw new InvalidOperationException("That model no longer exists.");

				ResetRun();
				await ApplyTrainingOptionsAsync(loaded.Options);
				TrainingRun.LoadModel(
					loaded.Network,
					loaded.ActionSpace,
					loaded.Options.Training.EpisodeTiming.EpisodeCount,
					loaded.Summary.EpisodesTrained
				);
				_selectedModelId = id;
				await RefreshHistoryAsync();
				return $"Loaded '{loaded.Summary.Name}' ({loaded.Summary.EpisodesTrained} episodes trained) -- press Start on the Run tab to train it further.";
			});

		private Task DeleteModelAsync(Guid id) =>
			ReportModelsAsync(async () =>
			{
				if (!await TrainedModelService.DeleteAsync(id))
					throw new InvalidOperationException("That model no longer exists.");

				await RefreshModelsAsync();
				return "Deleted the model and its history.";
			});

		private Task SelectModelAsync(Guid id) =>
			ReportModelsAsync(async () =>
			{
				_selectedModelId = id;
				await RefreshHistoryAsync();
				return null;
			});

		private Task SaveResultsAsync(string name) =>
			ReportModelsAsync(async () =>
			{
				var modelId = _selectedModelId ?? throw new InvalidOperationException("Select a model first.");
				var modelName = _savedModels.FirstOrDefault(m => m.Id == modelId)?.Name ?? "model";
				var resultsName = string.IsNullOrWhiteSpace(name)
					? $"{modelName} - {TrainingRun.EpisodesCompleted} eps - {DateTime.Now:yyyy-MM-dd HH:mm}"
					: name;

				await TrainingHistoryService.SaveAsync(modelId, resultsName, CurrentTrainingOptions, TrainingRun.CurrentRunSnapshot);
				await RefreshHistoryAsync();
				return $"Saved results '{resultsName.Trim()}' for '{modelName}'.";
			});

		private Task LoadResultsAsync(Guid id) =>
			ReportModelsAsync(async () =>
			{
				var data = await TrainingHistoryService.LoadAsync(id) ?? throw new InvalidOperationException("Those results no longer exist.");

				TrainingRun.ShowSavedResults(data);
				_activeTab = TrainV2Tab.Run;
				return null;
			});

		private Task DeleteResultsAsync(Guid id) =>
			ReportModelsAsync(async () =>
			{
				if (!await TrainingHistoryService.DeleteAsync(id))
					throw new InvalidOperationException("Those results no longer exist.");

				await RefreshHistoryAsync();
				return "Deleted the saved results.";
			});

		#endregion

		#region Options

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		// Round-trips through localStorage since this component's own fields don't survive
		// navigation; persisting is diffed against _lastPersistedOptionsJson to avoid write spam.
		private const string OptionsStorageKey = "poink.train2.options";

		private string? _lastPersistedOptionsJson;

		// The run outlives the page, so its viewing pace must too -- otherwise returning would slow a MAX run back to 1x.
		private const string LivePlaybackStorageKey = "poink.train2.live";

		private string? _lastPersistedLivePlaybackJson;

		private sealed record LivePlaybackPreferences(int SpeedIndex, bool BackgroundMode);

		private LivePlaybackPreferences CurrentLivePlayback => new(_liveSpeedIndex, _backgroundMode);

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				await LoadPersistedOptionsAsync();
				await LoadPersistedLivePlaybackAsync();
				return;
			}

			await PersistOptionsIfChangedAsync();
			await PersistLivePlaybackIfChangedAsync();
		}

		private async Task LoadPersistedLivePlaybackAsync()
		{
			var playback = await LocalStorage.GetItemAsync<LivePlaybackPreferences>(LivePlaybackStorageKey);
			if (playback is null)
				return;

			_liveSpeedIndex = Math.Clamp(playback.SpeedIndex, 0, LiveSpeedOptions.Length - 1);
			_backgroundMode = playback.BackgroundMode;
			if (_backgroundMode && _activeTab == TrainV2Tab.Live)
				_activeTab = TrainV2Tab.Run;
			_lastPersistedLivePlaybackJson = JsonSerializer.Serialize(CurrentLivePlayback);
			StateHasChanged();
		}

		private async Task PersistLivePlaybackIfChangedAsync()
		{
			var json = JsonSerializer.Serialize(CurrentLivePlayback);
			if (json == _lastPersistedLivePlaybackJson)
				return;

			_lastPersistedLivePlaybackJson = json;
			await LocalStorage.SetItemAsync(LivePlaybackStorageKey, CurrentLivePlayback);
		}

		private async Task LoadPersistedOptionsAsync()
		{
			var options = await LocalStorage.GetItemAsync<TrainingOptionsModel>(OptionsStorageKey);
			if (options is null)
				return;

			await ApplyTrainingOptionsAsync(options);

			_lastPersistedOptionsJson = JsonSerializer.Serialize(CurrentTrainingOptions);
			StateHasChanged();
		}

		private async Task ApplyTrainingOptionsAsync(TrainingOptionsModel options)
		{
			ApplyDataOptions(options.Data);
			ApplyFeatureOptions(options.Features);
			ApplyNetworkOptions(options.Network);
			ApplyOutputOptions(options.Output);
			ApplyTrainingTabOptions(options.Training);
			_barWidthSeconds = options.BarWidthSeconds;

			await ApplySelectedSymbolAsync(options.Data.SymbolId);
		}

		private async Task PersistOptionsIfChangedAsync()
		{
			var json = JsonSerializer.Serialize(CurrentTrainingOptions);
			if (json == _lastPersistedOptionsJson)
				return;

			_lastPersistedOptionsJson = json;
			await LocalStorage.SetItemAsync(OptionsStorageKey, CurrentTrainingOptions);
		}

		private TrainingOptionsModel CurrentTrainingOptions =>
			new(
				CurrentDataOptions,
				CurrentFeatureOptions,
				CurrentNetworkOptions,
				CurrentOutputOptions,
				CurrentTrainingTabOptions,
				_barWidthSeconds
			);

		#endregion
	}
}
