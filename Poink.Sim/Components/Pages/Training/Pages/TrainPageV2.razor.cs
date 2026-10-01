using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
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
		public void Dispose() => _runCancellation?.Cancel();

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

		// Background mode leaves Live with nothing current to show (see RunOneEpisode) -- hidden, not just disabled.
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
				TrainV2Tab.Live => "Watch the charts, equity curve and feature values for the episode currently in progress",
				_ => "",
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
		private bool _txFoldNormalize;

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
		private bool _includeVolumeOutput;
		private bool _includeStopLossOutput;
		private bool _includeTakeProfitOutput;

		private const int TotalActionOptionCount = 7;

		private int SelectedActionCount =>
			DirectionCount
			+ (_includeVolumeOutput ? 1 : 0)
			+ (_includeStopLossOutput ? 1 : 0)
			+ (_includeTakeProfitOutput ? 1 : 0);

		private bool AllActionsSelected => SelectedActionCount == TotalActionOptionCount;

		private void ToggleAllActions(ChangeEventArgs e)
		{
			var value = e.Value is true;
			_includeHold = value;
			_includeEnterLong = value;
			_includeEnterShort = value;
			_includeClose = value;
			_includeVolumeOutput = value;
			_includeStopLossOutput = value;
			_includeTakeProfitOutput = value;
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

		private int VolumeOutputWidth =>
			BrainCatalog.CreateActionOutputHead(ActionOutputKind.Volume, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

		private int StopLossOutputWidth =>
			BrainCatalog.CreateActionOutputHead(ActionOutputKind.StopLoss, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

		private int TakeProfitOutputWidth =>
			BrainCatalog.CreateActionOutputHead(ActionOutputKind.TakeProfit, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

		private int TotalOutputWidth =>
			DirectionCount
			+ (_includeVolumeOutput ? VolumeOutputWidth : 0)
			+ (_includeStopLossOutput ? StopLossOutputWidth : 0)
			+ (_includeTakeProfitOutput ? TakeProfitOutputWidth : 0);

		// Action Masking -- mirrors BrainBuilder's ActionsSection Action Masking card, but defaults
		// every rule off here rather than on. The XGrid-dependent rules are left out for now.
		private bool _maskPositionRequiredForClose;
		private bool _maskFlatRequiredForEntry;

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
			new(_includeHold, _includeEnterLong, _includeEnterShort, _includeClose, _includeVolumeOutput, _includeStopLossOutput, _includeTakeProfitOutput);

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
			_includeVolumeOutput = options.Actions.IncludeVolumeOutput;
			_includeStopLossOutput = options.Actions.IncludeStopLossOutput;
			_includeTakeProfitOutput = options.Actions.IncludeTakeProfitOutput;

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
				"Every episode runs over the Data tab's whole selected range, start to finish"),
			new(EpisodeSamplingMode.WindowSelected, "Window selected",
				"Every episode runs over the exact range marked with the LOOKBACK WINDOW tool on the chart below"),
		];

		private EpisodeSamplingMode _episodeSamplingMode = EpisodeSamplingMode.RandomFromSet;

		private List<ChartRangeFill> PreviewRangeFills() =>
			_previewWindowStart is { } start && _previewWindowEnd is { } end
				? [new ChartRangeFill(start, end, "LOOKBACK WINDOW", "volume")]
				: [];

		// The marked window itself is the episode's training set -- Visible bars is still a separate
		// lead-in DataProvider fetches before it, same as RandomFromSet.
		private int WindowTrainingBarCount =>
			_previewWindowStart is { } start && _previewWindowEnd is { } end
				? _previewBars.Count(b => b.OpenTime >= start && b.OpenTime <= end)
				: 0;

		private int WindowEpisodeBars => WindowTrainingBarCount + _visibleBars;

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

		private readonly List<string> _selectedRiskPolicies = [];
		private PositionControlOptionsModel _positionControl = new();

		[Inject]
		private IRiskPolicyService RiskPolicyService { get; set; } = default!;

		// Learning -- mirrors BrainBuilder's LearnSection, NEAT excluded from the strategy options.
		private static readonly IReadOnlyList<string> TrainingStrategyOptions = TrainingStrategyCatalog.Names;

		private static readonly string[] RewardPolicyOptions = ["Equity Drift (default)", "Hit Rate"];

		private string _trainingStrategyName = TrainingStrategyOptions[0];
		private string _rewardPolicyName = RewardPolicyOptions[0];
		private double _learningRate = 0.001;
		private bool _useEntropyBonus = true;

		// Decision Masking -- mirrors BrainBuilder's DecideSection. Gates whether the network is even
		// asked to decide, as opposed to Action Masking (Output tab) which restricts choices afterward.
		private sealed record DecisionMaskResolutionOption(DecisionMaskResolutionKind Kind, string Label, string Tooltip);

		private static readonly DecisionMaskResolutionOption[] DecisionMaskResolutionOptions =
		[
			new(DecisionMaskResolutionKind.PerBar, "Once per bar", "Check the rules once, when each bar closes"),
			new(DecisionMaskResolutionKind.EverySeconds, "Every X seconds", "Check the rules on a fixed time interval"),
			new(DecisionMaskResolutionKind.PerTick, "Every tick", "Check the rules on every incoming tick"),
			new(
				DecisionMaskResolutionKind.OnCellWidthChange,
				"On cell width change",
				"Check the rules whenever price crosses into a different fixed-width price cell"
			),
		];

		private DecisionMaskResolutionKind _decisionMaskResolution = DecisionMaskResolutionKind.PerBar;
		private double _decisionMaskResolutionSeconds = 1.0;
		private double _decisionMaskResolutionCellWidth = 0.10;
		private bool _maskDecisionPriceBelowPoc;

		private const int TotalDecisionMaskRuleCount = 1;

		private int SelectedDecisionMaskRuleCount => _maskDecisionPriceBelowPoc ? 1 : 0;

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
			new(_startingCash, _leverage, _commissionRatePercent, _stopOutLevelPercent, [.. _selectedRiskPolicies], _positionControl);

		private LearningOptionsModel CurrentLearningOptions => new(_trainingStrategyName, _rewardPolicyName, _learningRate, _useEntropyBonus);

		private DecisionMaskingOptionsModel CurrentDecisionMaskingOptions =>
			new(_decisionMaskResolution, _decisionMaskResolutionSeconds, _decisionMaskResolutionCellWidth, _maskDecisionPriceBelowPoc);

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
			_selectedRiskPolicies.Clear();
			if (traderSetup.SelectedRiskPolicies is { } policies)
				_selectedRiskPolicies.AddRange(policies);
			_positionControl = traderSetup.EffectiveControl;

			var learning = options.Learning;
			_trainingStrategyName = TrainingStrategyCatalog.Normalize(learning.TrainingStrategyName);
			_rewardPolicyName = learning.RewardPolicyName;
			_learningRate = learning.LearningRate;
			_useEntropyBonus = learning.UseEntropyBonus;

			var decisionMasking = options.DecisionMasking;
			_decisionMaskResolution = decisionMasking.DecisionMaskResolution;
			_decisionMaskResolutionSeconds = decisionMasking.DecisionMaskResolutionSeconds;
			_decisionMaskResolutionCellWidth = decisionMasking.DecisionMaskResolutionCellWidth;
			_maskDecisionPriceBelowPoc = decisionMasking.MaskDecisionPriceBelowPoc;
		}

		#endregion

		#region Run

		[Inject]
		private TrainingEngineService TrainingEngineService { get; set; } = default!;

		[Inject]
		private ActionAssembler ActionAssembler { get; set; } = default!;

		// Matches TrainingService's own windows (RecentRewardWindow/MaxMetricsLog), for consistent
		// UI behavior between the old and new pages.
		private const int RecentRewardWindow = 20;
		private const int MaxRetainedEpisodes = 500;

		private bool _isRunning;
		private bool _isPaused;
		private string? _runError;
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

		private CancellationTokenSource? _runCancellation;
		private MlpNetwork? _runNetwork;
		private ActionSpace? _runActionSpace;

		// Forces max speed and skips onDecision entirely -- see RunOneEpisode.
		private bool _backgroundMode;

		private void ToggleBackgroundMode()
		{
			_backgroundMode = !_backgroundMode;
			if (_backgroundMode && _activeTab == TrainV2Tab.Live)
				_activeTab = TrainV2Tab.Run;
		}

		// Render reads _episodeMetricsSnapshot (atomically-swapped, never mutated in place) instead
		// of this background-thread-mutated list, same pattern as LiveUiState.
		private readonly List<RlEpisodeMetric> _episodeMetrics = [];
		private IReadOnlyList<RlEpisodeMetric> _episodeMetricsSnapshot = [];
		private readonly Queue<double> _recentRewards = new();
		private readonly Queue<double> _recentEpisodeDurations = new();
		private int _episodesCompleted;
		private double _bestReward;
		private double _worstReward;
		private double _recentAverageReward;
		private double _episodesPerSecond;

		// Captured once per run session (see EnsureRunNetwork) so editing the Training tab's episode
		// count mid-run can't retarget the progress bar away from what RunTrainingLoopAsync captured.
		private int _runTargetEpisodeCount;

		private int TargetEpisodeCount =>
			_runTargetEpisodeCount > 0 ? _runTargetEpisodeCount : CurrentTrainingOptions.Training.EpisodeTiming.EpisodeCount;

		private double RunProgressPercent =>
			TargetEpisodeCount == 0 ? 0 : Math.Min(100.0, _episodesCompleted * 100.0 / TargetEpisodeCount);

		private double WinRatePercent => _episodeMetricsSnapshot.Count == 0 ? 0 : RlEpisodeMetricStats.WinRate(_episodeMetricsSnapshot);

		private double MedianReward => _episodeMetricsSnapshot.Count == 0 ? 0 : RlEpisodeMetricStats.Median(_episodeMetricsSnapshot);

		private double RewardStdDev => _episodeMetricsSnapshot.Count == 0 ? 0 : RlEpisodeMetricStats.StdDev(_episodeMetricsSnapshot);

		private double RewardSpread => _episodeMetrics.Count == 0 ? 0 : _bestReward - _worstReward;

		private void StartRun()
		{
			if (_isRunning)
				return;

			_isRunning = true;
			_isPaused = false;
			_runError = null;
			_runCancellation = new CancellationTokenSource();
			var options = CurrentTrainingOptions;
			var token = _runCancellation.Token;

			_ = Task.Run(() => RunTrainingLoopAsync(options, token));
		}

		private async Task RunTrainingLoopAsync(TrainingOptionsModel options, CancellationToken cancellationToken)
		{
			try
			{
				EnsureRunNetwork(options);

				var random = new Random();
				for (var i = 0; i < options.Training.EpisodeTiming.EpisodeCount; i++)
				{
					while (_isPaused)
					{
						if (cancellationToken.IsCancellationRequested)
							return;
						await Task.Delay(200, cancellationToken);
					}
					cancellationToken.ThrowIfCancellationRequested();

					await RunOneEpisode(options, random, cancellationToken);
					await InvokeAsync(StateHasChanged);
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when Stop cancels the token mid-run.
			}
			catch (Exception ex)
			{
				_runError = ex.Message;
			}
			finally
			{
				_isRunning = false;
				_isPaused = false;
				await InvokeAsync(StateHasChanged);
			}
		}

		private void EnsureRunNetwork(TrainingOptionsModel options)
		{
			if (_runNetwork is not null && _runActionSpace is not null)
				return;

			_runTargetEpisodeCount = options.Training.EpisodeTiming.EpisodeCount;
			var random = new Random();
			_runActionSpace = ActionAssembler.Assemble(options.Output);
			_runNetwork = TrainingEngineService.BuildNetwork(options, _runActionSpace, random);
		}

		private async Task RunOneEpisode(TrainingOptionsModel options, Random random, CancellationToken cancellationToken)
		{
			ResetLiveEpisodeState();
			var stopwatch = Stopwatch.StartNew();
			var episodeCount = TargetEpisodeCount;
			var progress = episodeCount > 1 ? (double)_episodesCompleted / (episodeCount - 1) : 1.0;
			var result = await TrainingEngineService.RunEpisode(
				options,
				_runNetwork!,
				_runActionSpace!,
				random,
				progress,
				_backgroundMode ? null : OnLiveDecision,
				_backgroundMode ? null : OnLiveTick,
				() => _backgroundMode ? TimeSpan.Zero : LiveTickDelay,
				() => _activeTab == TrainV2Tab.Live && _liveShowActions,
				() => _isPaused,
				cancellationToken
			);
			stopwatch.Stop();

			_episodesCompleted++;
			_bestReward = _episodesCompleted == 1 ? result.Reward : Math.Max(_bestReward, result.Reward);
			_worstReward = _episodesCompleted == 1 ? result.Reward : Math.Min(_worstReward, result.Reward);

			_recentRewards.Enqueue(result.Reward);
			if (_recentRewards.Count > RecentRewardWindow)
				_recentRewards.Dequeue();
			_recentAverageReward = _recentRewards.Average();

			_recentEpisodeDurations.Enqueue(stopwatch.Elapsed.TotalSeconds);
			if (_recentEpisodeDurations.Count > RecentRewardWindow)
				_recentEpisodeDurations.Dequeue();
			var durationSum = _recentEpisodeDurations.Sum();
			_episodesPerSecond = durationSum > 0 ? _recentEpisodeDurations.Count / durationSum : 0;

			_episodeMetrics.Add(new RlEpisodeMetric(_episodesCompleted, result.Reward, result.FinalCapital.Equity));
			if (_episodeMetrics.Count > MaxRetainedEpisodes)
				_episodeMetrics.RemoveAt(0);
			_episodeMetricsSnapshot = _episodeMetrics.ToArray();
		}

		private async Task StepRun()
		{
			if (_isRunning && !_isPaused)
				return;

			try
			{
				var options = CurrentTrainingOptions;
				EnsureRunNetwork(options);
				_runCancellation = new CancellationTokenSource();
				await RunOneEpisode(options, new Random(), _runCancellation.Token);
				_runError = null;
			}
			catch (OperationCanceledException)
			{
				// Expected when Reset cancels mid-step.
			}
			catch (Exception ex)
			{
				_runError = ex.Message;
			}
		}

		private void ResetRun()
		{
			_runCancellation?.Cancel();
			_isRunning = false;
			_isPaused = false;
			_runNetwork = null;
			_runActionSpace = null;
			_runTargetEpisodeCount = 0;
			_episodeMetrics.Clear();
			_episodeMetricsSnapshot = [];
			_recentRewards.Clear();
			_recentEpisodeDurations.Clear();
			_episodesCompleted = 0;
			_bestReward = 0;
			_worstReward = 0;
			_recentAverageReward = 0;
			_episodesPerSecond = 0;
			_runError = null;
			ResetLiveEpisodeState();
		}

		private void PauseRun() => _isPaused = true;

		private void ResumeRun() => _isPaused = false;

		private void StopRun()
		{
			_runCancellation?.Cancel();
			_isRunning = false;
			_isPaused = false;
		}

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

			var bars = previous?.Bars ?? [];
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
			if (live is null || _runActionSpace is not { } actionSpace)
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
			if (_isPaused)
				ResumeRun();
			else if (_isRunning)
				PauseRun();
			else
				StartRun();
		}

		#endregion

		#region Options

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		// Round-trips through localStorage since this component's own fields don't survive
		// navigation; persisting is diffed against _lastPersistedOptionsJson to avoid write spam.
		private const string OptionsStorageKey = "poink.train2.options";

		private string? _lastPersistedOptionsJson;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				await LoadPersistedOptionsAsync();
				return;
			}

			await PersistOptionsIfChangedAsync();
		}

		private async Task LoadPersistedOptionsAsync()
		{
			var options = await LocalStorage.GetItemAsync<TrainingOptionsModel>(OptionsStorageKey);
			if (options is null)
				return;

			ApplyDataOptions(options.Data);
			ApplyFeatureOptions(options.Features);
			ApplyNetworkOptions(options.Network);
			ApplyOutputOptions(options.Output);
			ApplyTrainingTabOptions(options.Training);
			_barWidthSeconds = options.BarWidthSeconds;

			await ApplySelectedSymbolAsync(options.Data.SymbolId);

			_lastPersistedOptionsJson = JsonSerializer.Serialize(CurrentTrainingOptions);
			StateHasChanged();
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
