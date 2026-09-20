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
		private bool _includeTraderState;

		private bool _traderStateIncludePosition = true;
		private bool _traderStateIncludePositionPrice = true;
		private bool _traderStateIncludePositionTicks = true;
		private bool _traderStateIncludePositionDistance = true;
		private bool _traderStateIncludeCurrentPrice = true;
		private bool _traderStateIncludeCash = true;
		private bool _traderStateIncludeRealized = true;
		private bool _traderStateIncludeUnrealized = true;
		private bool _traderStateIncludeEquity = true;
		private bool _traderStateIncludeMargin = true;
		private bool _traderStateIncludeMarginLevel = true;
		private bool _traderStateIncludePositionCount = true;

		private bool _includeTraderStateOutcomes;
		private bool _includeTraderStatePositionLevel;
		private bool _includeTraderStateDirection;

		private int _traderStateOutcomeListLength = 5;
		private int _traderStatePositionLevelListLength = 5;
		private int _traderStateDirectionListLength = 5;

		private sealed record TraderStateFieldRow(string Label, string Tooltip, Func<bool> Get, Action<bool> Set);

		private IReadOnlyList<TraderStateFieldRow> TraderStateFieldRows =>
			new List<TraderStateFieldRow>
			{
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
				new(
					"Position Count",
					"1 if a position is open, 0 if flat",
					() => _traderStateIncludePositionCount,
					v => _traderStateIncludePositionCount = v
				),
			};

		private const int TotalTraderStateFieldCount = 12;

		private int SelectedTraderStateFieldCount => TraderStateFieldRows.Count(r => r.Get());

		private bool AllTraderStateFieldsSelected => SelectedTraderStateFieldCount == TotalTraderStateFieldCount;

		private void ToggleAllTraderStateFields(ChangeEventArgs e)
		{
			var value = e.Value is true;
			foreach (var row in TraderStateFieldRows)
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

		// One column per hidden layer, one dot per unit (capped visually), fully connected mesh between
		// adjacent columns -- deliberately its own visual, not TrainingPreview's per-layer bar chart.
		private const int MaxVisibleDots = 7;

		private sealed record NetworkDot(double X, double Y, int? MoreCount);

		private sealed record NetworkColumn(string Label, int Size, List<NetworkDot> Dots);

		private sealed record NetworkEdge(double X1, double Y1, double X2, double Y2);

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

				var columns = new List<NetworkColumn>();
				for (var i = 0; i < layers.Count; i++)
				{
					var (label, size) = layers[i];
					if (size <= 0)
						continue;
					var x = layers.Count == 1 ? 50 : 15 + i / (double)(layers.Count - 1) * 70;
					columns.Add(new NetworkColumn(label, size, BuildDots(x, size)));
				}
				return columns;
			}
		}

		private static List<NetworkDot> BuildDots(double x, int size)
		{
			var hasMore = size > MaxVisibleDots;
			var shown = hasMore ? MaxVisibleDots - 1 : size;

			var dots = new List<NetworkDot>();
			for (var i = 0; i < shown; i++)
			{
				var y = shown == 1 ? 50 : 15 + i / (double)(shown - 1) * 70;
				dots.Add(new NetworkDot(x, y, null));
			}
			if (hasMore)
				dots.Add(new NetworkDot(x, 90, size - shown));
			return dots;
		}

		private List<NetworkEdge> NetworkEdges
		{
			get
			{
				var columns = NetworkColumns;
				var edges = new List<NetworkEdge>();
				for (var i = 0; i < columns.Count - 1; i++)
				{
					var from = columns[i].Dots.Where(d => d.MoreCount is null);
					var to = columns[i + 1].Dots.Where(d => d.MoreCount is null);
					foreach (var a in from)
					foreach (var b in to)
						edges.Add(new NetworkEdge(a.X, a.Y, b.X, b.Y));
				}
				return edges;
			}
		}

		private static string Coord(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

		// <text> can't carry attributes directly in a .razor file (RZ1023), so it's built as markup here instead.
		private static string MoreDotSvg(NetworkDot dot) =>
			$"<text x=\"{Coord(dot.X)}\" y=\"{Coord(dot.Y)}\" class=\"trv2-network-more\">+{dot.MoreCount}</text>";

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
		private int _episodeBars = 20;
		private int _barWidthSeconds = 60;
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

		// When restricted, Bars/episode derives from session length / bar width instead of being picked directly.
		private int EffectiveEpisodeBars =>
			_restrictEpisodeTiming ? (int)((_sessionEnd - _sessionStart).TotalSeconds / _barWidthSeconds) : _episodeBars;

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
