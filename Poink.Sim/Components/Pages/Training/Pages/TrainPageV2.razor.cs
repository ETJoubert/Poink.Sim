using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Services.Symbols;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
	public partial class TrainPageV2 : ComponentBase
	{
		#region Tabs

		private enum TrainV2Tab
		{
			Data,
			Features,
			Network,
			Output,
			Training,
			Run,
			Playback,
		}

		private static readonly TrainV2Tab[] Tabs =
		[
			TrainV2Tab.Data,
			TrainV2Tab.Features,
			TrainV2Tab.Network,
			TrainV2Tab.Output,
			TrainV2Tab.Training,
			TrainV2Tab.Run,
			TrainV2Tab.Playback,
		];

		private TrainV2Tab _activeTab = TrainV2Tab.Data;

		private static string TabTitle(TrainV2Tab tab) =>
			tab switch
			{
				TrainV2Tab.Data => "What data is pulled, for which symbol and range",
				TrainV2Tab.Features => "Which encoders transform pulled data into the input vector",
				TrainV2Tab.Network => "The architecture that consumes the feature vector",
				TrainV2Tab.Output => "How the network's raw outputs map to trade actions",
				TrainV2Tab.Training => "How episodes are timed/sampled, and the trader each one starts with",
				TrainV2Tab.Run => "Start/pause/step the run and review live/saved results",
				TrainV2Tab.Playback => "Replay a single episode bar by bar across the chart panels below",
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

		// Trader State -- mirrors BrainBuilder's FeaturesSection Trader State card.
		private bool _traderStateIncludePosition;
		private bool _traderStateIncludePositionPrice;
		private bool _traderStateIncludePositionTicks;
		private bool _traderStateIncludePositionDistance;
		private bool _traderStateIncludeCurrentPrice;
		private bool _traderStateIncludeCash;
		private bool _traderStateIncludeRealized;
		private bool _traderStateIncludeUnrealized;
		private bool _traderStateIncludeEquity;
		private bool _traderStateIncludeMargin;
		private bool _traderStateIncludeMarginLevel;
		private bool _traderStateIncludePositionCount;

		private bool _includeTraderStateOutcomes;
		private bool _includeTraderStatePositionLevel;
		private bool _includeTraderStateDirection;

		private int _traderStateOutcomeListLength = 5;
		private int _traderStatePositionLevelListLength = 5;
		private int _traderStateDirectionListLength = 5;

		private sealed record TraderStateFieldRow(string Label, string Tooltip, Func<bool> Get, Action<bool> Set);

		private IReadOnlyList<TraderStateFieldRow> PositionFieldRows =>
			[
				new(
					"Position",
					"Direction of the open position: 1 long, -1 short, 0 flat",
					() => _traderStateIncludePosition,
					v => _traderStateIncludePosition = v
				),
				new(
					"Position Price",
					"Entry price of the open position, 0 if flat",
					() => _traderStateIncludePositionPrice,
					v => _traderStateIncludePositionPrice = v
				),
				new(
					"Position Ticks",
					"Ticks the open position has been held, 0 if flat",
					() => _traderStateIncludePositionTicks,
					v => _traderStateIncludePositionTicks = v
				),
				new(
					"Position Distance",
					"Cumulative price distance traveled since the position opened, 0 if flat",
					() => _traderStateIncludePositionDistance,
					v => _traderStateIncludePositionDistance = v
				),
				new(
					"Current Price",
					"Current market price",
					() => _traderStateIncludeCurrentPrice,
					v => _traderStateIncludeCurrentPrice = v
				),
				new(
					"Position Count",
					"1 if a position is open, 0 if flat",
					() => _traderStateIncludePositionCount,
					v => _traderStateIncludePositionCount = v
				),
			];

		private IReadOnlyList<TraderStateFieldRow> AccountFieldRows =>
			[
				new("Cash", "Available cash balance", () => _traderStateIncludeCash, v => _traderStateIncludeCash = v),
				new(
					"Realized",
					"Realized profit/loss booked so far",
					() => _traderStateIncludeRealized,
					v => _traderStateIncludeRealized = v
				),
				new(
					"Unrealized",
					"Unrealized profit/loss on the open position",
					() => _traderStateIncludeUnrealized,
					v => _traderStateIncludeUnrealized = v
				),
				new("Equity", "Cash plus unrealized profit/loss", () => _traderStateIncludeEquity, v => _traderStateIncludeEquity = v),
				new(
					"Margin",
					"Margin currently used by the open position",
					() => _traderStateIncludeMargin,
					v => _traderStateIncludeMargin = v
				),
				new(
					"Margin Level",
					"Equity as a percent of margin used",
					() => _traderStateIncludeMarginLevel,
					v => _traderStateIncludeMarginLevel = v
				),
			];

		private IReadOnlyList<TraderStateFieldRow> TraderStateFieldRows => [.. PositionFieldRows, .. AccountFieldRows];

		private const int TotalTraderStateFieldCount = 12;

		private int SelectedTraderStateFieldCount => TraderStateFieldRows.Count(r => r.Get());

		private static bool AllFieldsSelected(IReadOnlyList<TraderStateFieldRow> rows) => rows.All(r => r.Get());

		private static void SetAllFields(IReadOnlyList<TraderStateFieldRow> rows, ChangeEventArgs e)
		{
			var value = e.Value is true;
			foreach (var row in rows)
				row.Set(value);
		}

		private void OnTraderStateFieldChanged(TraderStateFieldRow row, ChangeEventArgs e) => row.Set(e.Value is true);

		// TxFold -- the real TxFold chart component owns its own toolbar/selection (bound here so the
		// Features grid below can read it); the grid itself shows field labels only, no fetched data.
		private HashSet<int> _txFoldSelectedSeconds = [60, 120, 240, 480, 900, 1800, 3600, 7200];
		private BarAggregationKind _txFoldBarKind = BarAggregationKind.Ohlc;

		private static string TxFoldTimeframeLabel(int seconds) =>
			SnapshotTimeframeOptions.All.FirstOrDefault(o => o.Seconds == seconds).Label;

		// Every kind stores exactly 4 values per bar -- Quartile/Decile/MeanStdDev just relabel the
		// same 4 slots (see TxFoldStats.BuildBar) rather than adding a 5th.
		private static string[] TxFoldFieldLabels(BarAggregationKind kind) =>
			kind switch
			{
				BarAggregationKind.Quartile => ["Q1", "Max", "Min", "Q3"],
				BarAggregationKind.Decile => ["D1", "Max", "Min", "D9"],
				BarAggregationKind.MeanStdDev => ["Mean", "+SD", "-SD", "Mean"],
				_ => ["O", "H", "L", "C"],
			};

		// Picked on the Features tab because it sets XGrid's window; the Training tab reads it for Episode span.
		private int _barWidthSeconds = 60;

		private const int TxFoldValuesPerTimeframe = 4;

		private int TxFoldFeatureCount => _txFoldSelectedSeconds.Count * TxFoldValuesPerTimeframe;

		private int TraderStateFeatureCount =>
			SelectedTraderStateFieldCount
			+ (_includeTraderStateOutcomes ? _traderStateOutcomeListLength : 0)
			+ (_includeTraderStatePositionLevel ? _traderStatePositionLevelListLength : 0)
			+ (_includeTraderStateDirection ? _traderStateDirectionListLength : 0);

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
		// every rule off here rather than on.
		private bool _maskPositionRequiredForClose;
		private bool _maskFlatRequiredForEntry;
		private bool _maskLongRequiresUpSignal;
		private bool _maskShortRequiresDownSignal;
		private bool _maskKeyLevelRequiredForClose;

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
				new(
					"Long Requires Up Signal",
					"Enter Long is illegal unless the xGrid Rank Top % Up signal is active -- has no effect without the XGrid Signals encoder",
					() => _maskLongRequiresUpSignal,
					v => _maskLongRequiresUpSignal = v
				),
				new(
					"Short Requires Down Signal",
					"Enter Short is illegal unless the xGrid Rank Top % Down signal is active -- has no effect without the XGrid Signals encoder",
					() => _maskShortRequiresDownSignal,
					v => _maskShortRequiresDownSignal = v
				),
				new(
					"Close Requires Key Level",
					"Close is illegal unless price is currently at POC, VAH, VAL, or VWAP -- has no effect without the XGrid Signals encoder",
					() => _maskKeyLevelRequiredForClose,
					v => _maskKeyLevelRequiredForClose = v
				),
			};

		private void OnMaskRuleFieldChanged(MaskRuleRow row, ChangeEventArgs e) => row.Set(e.Value is true);

		private const int TotalMaskRuleCount = 5;

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

		#endregion

		#region Training

		// Episode Timing -- mirrors TrainPage's Episode Timing panel.
		private int _episodeCount = 1000;
		private int _trainingBars = 20;
		private int _visibleBars = 120;
		private SimulationSteppingMode _steppingMode = SimulationSteppingMode.Bar;
		private double _cellWidth = 0.10;

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

		// Cell stepping fires on price moves rather than the clock, so its episode has no fixed duration.
		private TimeSpan? EpisodeSpan =>
			_steppingMode == SimulationSteppingMode.Bar ? TimeSpan.FromSeconds((double)EpisodeBars * _barWidthSeconds) : null;

		private static string FormatOptionalWindow(TimeSpan? window) => window is { } value ? FormatWindow(value) : "-";

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

		[Inject]
		private IRiskPolicyService RiskPolicyService { get; set; } = default!;

		// Learning -- mirrors BrainBuilder's LearnSection, NEAT excluded from the strategy options.
		private static readonly string[] TrainingStrategyOptions =
		[
			"Policy Gradient (REINFORCE)",
			"Policy Gradient (Per-Decision)",
		];

		private static readonly string[] RewardPolicyOptions = ["Equity Drift (default)", "Hit Rate"];

		private string _trainingStrategyName = TrainingStrategyOptions[0];
		private string _rewardPolicyName = RewardPolicyOptions[0];
		private double _learningRate = 0.001;

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

		#endregion

		#region Run

		// Wireframe only -- no ITrainingService wiring yet, so the panel/charts render against an
		// empty placeholder rather than a live run.
		private const double PlaceholderMetric = 0;
		private static readonly List<RlEpisodeMetric> _placeholderEpisodes = [];

		// Run controls -- mirrors TrainPage's Start/Step/Reset/Pause/Resume/Stop toolbar, not wired
		// to a real run.
		private bool _isRunning;
		private bool _isPaused;

		private void StartRun()
		{
			_isRunning = true;
			_isPaused = false;
		}

		private void StepRun() { }

		private void ResetRun()
		{
			_isRunning = false;
			_isPaused = false;
		}

		private void PauseRun() => _isPaused = true;

		private void ResumeRun() => _isPaused = false;

		private void StopRun()
		{
			_isRunning = false;
			_isPaused = false;
		}

		#endregion

		#region Playback

		// Wireframe only -- mirrors EpisodePlayback's controls/panel toggles, not wired to a real replay.
		private bool _isPlaying;

		private static readonly double[] PlaybackSpeedOptions = { 0.25, 0.5, 1, 2, 5, 10, 25 };

		private int _playbackSpeedIndex = 2;

		private bool _showPlaybackMarketChart = true;
		private bool _showPlaybackXGrid = true;
		private bool _showPlaybackTxFold = true;
		private bool _showPlaybackEquityCurve = true;

		private void TogglePlayback() => _isPlaying = !_isPlaying;

		private void DecreasePlaybackSpeed() => _playbackSpeedIndex = Math.Max(0, _playbackSpeedIndex - 1);

		private void IncreasePlaybackSpeed() =>
			_playbackSpeedIndex = Math.Min(PlaybackSpeedOptions.Length - 1, _playbackSpeedIndex + 1);

		private static string FormatPlaybackSpeed(double speed) =>
			(speed < 1 ? speed.ToString("0.##", CultureInfo.InvariantCulture) : speed.ToString("0", CultureInfo.InvariantCulture)) + "x";

		#endregion
	}
}
