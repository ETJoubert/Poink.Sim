using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class EpisodePlayback : ComponentBase, IDisposable, IAsyncDisposable
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Inject]
		private IBrainService BrainService { get; set; } = default!;

		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Parameter]
		public string BrainName { get; set; } = "";

		private BrainRecipe? Recipe => BrainService.GetRecipe(BrainName);

		private bool _trainingWasAdvancing;
		private bool _autoShownXGridForCell;

		protected override void OnInitialized()
		{
			_trainingWasAdvancing = Service.IsRunning && !Service.IsPaused;
			Service.OnChanged += HandleServiceChanged;
		}

		private void HandleServiceChanged()
		{
			bool isAdvancing = Service.IsRunning && !Service.IsPaused;
			if (isAdvancing && !_trainingWasAdvancing)
				StopAndResetReplay();
			_trainingWasAdvancing = isAdvancing;

			// One-time reveal, not a standing override -- a Cell-mode episode is worth
			// seeing the grid for, but a user who hides it again afterward is respected.
			if (!_autoShownXGridForCell && Service.LatestEpisodeSteppingMode == SimulationSteppingMode.Cell)
			{
				_showXGrid = true;
				_autoShownXGridForCell = true;
			}

			_ = InvokeAsync(StateHasChanged);
		}

		public void Dispose()
		{
			Service.OnChanged -= HandleServiceChanged;
			_replayTimer?.Dispose();
		}

		private ElementReference _episodeChartHDividerRef;
		private IJSObjectReference? _episodeChartResizeInstance;

		// Drag-to-resize the episode chart row's height, same mechanism as SimulationPage's
		// ChartDividerScript, minus the width half since panels here split width equally.
		private const string EpisodeChartResizeScript = """
			(function() {
			    function bindHeight(divider) {
			        if (!divider || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minHeight = parseInt(divider.dataset.minHeight, 10) || 160;
			        var maxHeightVh = parseInt(divider.dataset.maxHeightVh, 10) || 80;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var target = divider.previousElementSibling;
			            if (!target) return;
			            var startY = e.clientY;
			            var startHeight = target.getBoundingClientRect().height;
			            var maxHeight = (maxHeightVh / 100) * window.innerHeight;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('train-episode-hdivider-active');

			            function clamp(ev) {
			                return Math.min(maxHeight, Math.max(minHeight, startHeight + (ev.clientY - startY)));
			            }
			            function onMove(ev) {
			                target.style.height = clamp(ev) + 'px';
			            }
			            function onUp(ev) {
			                divider.releasePointerCapture(e.pointerId);
			                divider.removeEventListener('pointermove', onMove);
			                divider.removeEventListener('pointerup', onUp);
			                divider.classList.remove('train-episode-hdivider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    return { bindHeight: bindHeight };
			})()
			""";

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
				_episodeChartResizeInstance = await JS.InvokeAsync<IJSObjectReference>(
					"eval",
					EpisodeChartResizeScript
				);

			if (EpisodeBarRows.Count > 0 && _episodeChartResizeInstance is not null)
				await _episodeChartResizeInstance.InvokeVoidAsync("bindHeight", _episodeChartHDividerRef);
		}

		public async ValueTask DisposeAsync()
		{
			if (_episodeChartResizeInstance is not null)
			{
				try
				{
					await _episodeChartResizeInstance.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
			}
		}

		private bool _liveFeedbackPaused;
		private LiveFeedbackSnapshot? _liveFeedbackSnapshot;

		private sealed record LiveFeedbackSnapshot(
			IReadOnlyList<RlEpisodeMetric> RecentEpisodes,
			IReadOnlyList<RlEpisodeTrades> RecentEpisodeTrades,
			RlActionCounts ActionCounts,
			IReadOnlyList<HlocAvdtBar> LatestEpisodeBars,
			IReadOnlyList<BarPriceHistogram> LatestEpisodeBarHistograms,
			int LatestEpisodeWarmupBars,
			SimulationSteppingMode LatestEpisodeSteppingMode,
			double LatestEpisodeCellWidth,
			IReadOnlyList<Trade> LatestEpisodeTrades,
			IReadOnlyList<double> LatestEpisodeBarEquity,
			IReadOnlyList<RlBarAction> LatestEpisodeBarActions,
			IReadOnlyList<IReadOnlyList<RlInputFeature>> LatestEpisodeBarInputs
		);

		private void ToggleLiveFeedback()
		{
			if (_liveFeedbackPaused)
			{
				_liveFeedbackPaused = false;
				_liveFeedbackSnapshot = null;
				return;
			}

			_liveFeedbackSnapshot = new LiveFeedbackSnapshot(
				Service.RecentEpisodes,
				Service.RecentEpisodeTrades,
				Service.ActionCounts,
				Service.LatestEpisodeBars,
				Service.LatestEpisodeBarHistograms,
				Service.LatestEpisodeWarmupBars,
				Service.LatestEpisodeSteppingMode,
				Service.LatestEpisodeCellWidth,
				Service.LatestEpisodeTrades,
				Service.LatestEpisodeBarEquity,
				Service.LatestEpisodeBarActions,
				Service.LatestEpisodeBarInputs
			);
			_liveFeedbackPaused = true;
		}

		private bool SourceIsRunning => !_liveFeedbackPaused && Service.IsRunning;
		private IReadOnlyList<RlEpisodeMetric> SourceChartEpisodes =>
			_liveFeedbackSnapshot?.RecentEpisodes ?? Service.RecentEpisodes;
		private IReadOnlyList<RlEpisodeTrades> SourceRecentEpisodeTrades =>
			_liveFeedbackSnapshot?.RecentEpisodeTrades ?? Service.RecentEpisodeTrades;
		private RlActionCounts SourceActionCounts => _liveFeedbackSnapshot?.ActionCounts ?? Service.ActionCounts;
		private IReadOnlyList<HlocAvdtBar> SourceEpisodeBars =>
			_liveFeedbackSnapshot?.LatestEpisodeBars ?? Service.LatestEpisodeBars;
		private IReadOnlyList<BarPriceHistogram> SourceEpisodeBarHistograms =>
			_liveFeedbackSnapshot?.LatestEpisodeBarHistograms ?? Service.LatestEpisodeBarHistograms;
		private int SourceEpisodeWarmupBars =>
			_liveFeedbackSnapshot?.LatestEpisodeWarmupBars ?? Service.LatestEpisodeWarmupBars;
		private SimulationSteppingMode SourceEpisodeSteppingMode =>
			_liveFeedbackSnapshot?.LatestEpisodeSteppingMode ?? Service.LatestEpisodeSteppingMode;
		private double SourceEpisodeCellWidth =>
			_liveFeedbackSnapshot?.LatestEpisodeCellWidth ?? Service.LatestEpisodeCellWidth;
		private IReadOnlyList<Trade> SourceEpisodeTrades =>
			_liveFeedbackSnapshot?.LatestEpisodeTrades ?? Service.LatestEpisodeTrades;
		private IReadOnlyList<double> SourceEpisodeBarEquity =>
			_liveFeedbackSnapshot?.LatestEpisodeBarEquity ?? Service.LatestEpisodeBarEquity;
		private IReadOnlyList<RlBarAction> SourceEpisodeBarActions =>
			_liveFeedbackSnapshot?.LatestEpisodeBarActions ?? Service.LatestEpisodeBarActions;
		private IReadOnlyList<IReadOnlyList<RlInputFeature>> SourceEpisodeBarInputs =>
			_liveFeedbackSnapshot?.LatestEpisodeBarInputs ?? Service.LatestEpisodeBarInputs;

		private List<CTraderBarRow> EpisodeBarRows =>
			SourceEpisodeBars
				.Select(b => new CTraderBarRow(
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

		private IReadOnlyList<HlocAvdtBar> DisplayedEpisodeBarsRaw =>
			_replayRawBars is not null ? _replayRawBars.Take(_revealedBarCount).ToList() : SourceEpisodeBars;

		private IReadOnlyList<BarPriceHistogram> DisplayedEpisodeBarHistograms =>
			_replayHistograms is not null
				? _replayHistograms.Take(_revealedBarCount).ToList()
				: SourceEpisodeBarHistograms;

		private EncoderSizeOptions EncoderSizes => Recipe?.EncoderSizes ?? new EncoderSizeOptions();

		private double? EpisodeLastPrice =>
			DisplayedEpisodeBarsRaw.Count > 0 ? DisplayedEpisodeBarsRaw[^1].Close : null;

		private List<WeightedPriceSample> EpisodeVolumeProfileSamples
		{
			get
			{
				var histograms = DisplayedEpisodeBarHistograms;
				int window =
					EncoderSizes.VolumeProfileLookbackBars > 0
						? EncoderSizes.VolumeProfileLookbackBars
						: histograms.Count;
				return BarPriceHistogram
					.Merge(histograms, window)
					.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
					.ToList();
			}
		}

		private List<WeightedTimeSample> EpisodeTimeProfileSamples
		{
			get
			{
				var bars = DisplayedEpisodeBarsRaw;
				var histograms = DisplayedEpisodeBarHistograms;
				int window =
					EncoderSizes.TimeProfileLookbackBars > 0 ? EncoderSizes.TimeProfileLookbackBars : histograms.Count;
				return BarPriceHistogram
					.MergeTime(bars, histograms, window)
					.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
					.ToList();
			}
		}

		private bool _showMarketChart = true;
		private bool _showVolumeProfile = true;
		private bool _showTimeProfile = true;
		private bool _showEquityCurve = true;
		private bool _showXGrid;

		private int _xgridPipCount = 20;
		private int _xgridColCount = 20;

		private List<WeightedPriceSample> EpisodeXGridSamples
		{
			get
			{
				var histograms = DisplayedEpisodeBarHistograms;
				int window =
					EncoderSizes.XGridWindowBars > 0
						? Math.Min(EncoderSizes.XGridWindowBars, histograms.Count)
						: histograms.Count;
				return BarPriceHistogram
					.Merge(histograms, window)
					.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
					.ToList();
			}
		}

		// Window and pip size mirror what XGridSignalEncoder itself trains with, same
		// "displayed panel mirrors what the network sees" convention as EpisodeVolumeProfileSamples.
		private XGridResult? EpisodeXGrid
		{
			get
			{
				var bars = DisplayedEpisodeBarsRaw;
				var histograms = DisplayedEpisodeBarHistograms;
				if (bars.Count == 0 || histograms.Count == 0)
					return null;

				int window = Math.Min(
					EncoderSizes.XGridWindowBars > 0 ? EncoderSizes.XGridWindowBars : bars.Count,
					bars.Count
				);
				var start = bars[bars.Count - window].OpenTime;
				var end = bars[^1].OpenTime;
				return XGridStats.Compute(
					bars,
					histograms,
					EpisodeXGridSamples,
					start,
					end,
					_xgridPipCount * EncoderSizes.XGridPipSize,
					_xgridColCount
				);
			}
		}

		private static readonly double[] ReplaySpeedOptions = { 0.25, 0.5, 1, 2, 5, 10, 25 };

		private const double BaseBarsPerSecond = 2.0;

		private int _replaySpeedIndex = 2;
		private List<CTraderBarRow>? _replayBars;
		private List<Trade>? _replayTrades;
		private List<double>? _replayBarEquity;
		private List<RlBarAction>? _replayBarActions;
		private List<HlocAvdtBar>? _replayRawBars;
		private List<BarPriceHistogram>? _replayHistograms;
		private List<IReadOnlyList<RlInputFeature>>? _replayBarInputs;

		private int _replayWarmupBars;
		private SimulationSteppingMode _replaySteppingMode;
		private double _replayCellWidth;
		private int _revealedBarCount;
		private bool _isReplaying;
		private Timer? _replayTimer;

		private double ReplaySpeed =>
			ReplaySpeedOptions[Math.Clamp(_replaySpeedIndex, 0, ReplaySpeedOptions.Length - 1)];
		private int ReplayIntervalMs => Math.Max(10, (int)(1000.0 / (BaseBarsPerSecond * ReplaySpeed)));

		private bool CanReplay => EpisodeBarRows.Count > 0;

		private bool ReplayHasPausedProgress =>
			_replayBars is not null && _revealedBarCount > 0 && _revealedBarCount < _replayBars.Count;

		private List<CTraderBarRow> DisplayedEpisodeBars =>
			_replayBars is not null ? _replayBars.Take(_revealedBarCount).ToList() : EpisodeBarRows;

		private List<Trade> DisplayedRevealedTrades
		{
			get
			{
				var trades = _replayBars is not null ? _replayTrades ?? new List<Trade>() : SourceEpisodeTrades;
				if (trades.Count == 0)
					return new List<Trade>();

				var visibleUntil =
					_replayBars is null ? DateTime.MaxValue
					: _revealedBarCount > 0 ? _replayBars[_revealedBarCount - 1].TimeUtc
					: DateTime.MinValue;

				return trades.Where(t => t.ExitTime <= visibleUntil).ToList();
			}
		}

		private List<ChartDiagonalLine> DisplayedTradeLines =>
			DisplayedRevealedTrades
				.Select(t => new ChartDiagonalLine(
					t.EntryTime,
					t.EntryPrice,
					t.ExitTime,
					t.ExitPrice,
					ColorGroup: t.RealizedPnl >= 0 ? "win" : "loss"
				))
				.ToList();

		private List<XGridPosition> DisplayedXGridPositions =>
			DisplayedRevealedTrades
				.Select(t => new XGridPosition(
					MapDirection(t.Direction),
					t.EntryTime,
					t.EntryPrice,
					t.ExitTime,
					t.ExitPrice
				))
				.ToList();

		private static XGridPositionDirection MapDirection(PositionDirection direction) =>
			direction == PositionDirection.Long ? XGridPositionDirection.Long : XGridPositionDirection.Short;

		// Matches this brain's own XGrid Signals encoder settings -- lets the panel show
		// exactly the grid the network decides from, instead of an independently tuned one.
		private void MatchXGridToBrainConfig()
		{
			_xgridPipCount = Math.Max(1, EncoderSizes.XGridRowCount);
			_xgridColCount = Math.Max(1, EncoderSizes.XGridColCount);
		}

		private TradeStatistics DisplayedTraderStats => TradeStatistics.From(DisplayedRevealedTrades, 0, 0);

		private (int Longs, int Shorts, int Closes) DisplayedBlockedCounts
		{
			get
			{
				var bars = DisplayedEpisodeBars;
				var actions = DisplayedBarActionsSource;
				if (actions.Count == 0 || bars.Count == 0)
					return (0, 0, 0);

				// Actions are chronological, so a decision is "revealed" once its own timestamp
				// falls within the bars shown so far, for any number of decisions per bar.
				DateTime cutoff = bars[^1].TimeUtc;
				int longs = 0,
					shorts = 0,
					closes = 0;
				foreach (var action in actions)
				{
					if (action.Timestamp > cutoff)
						break;
					if (!action.Blocked)
						continue;
					if (action.Action == SimulationAction.EnterLong)
						longs++;
					else if (action.Action == SimulationAction.EnterShort)
						shorts++;
					else if (action.Action == SimulationAction.Close)
						closes++;
				}
				return (longs, shorts, closes);
			}
		}

		private static string DecisionLabel(SimulationAction? action) =>
			action switch
			{
				SimulationAction.Hold => "HOLD",
				SimulationAction.EnterLong => "LONG",
				SimulationAction.EnterShort => "SHORT",
				SimulationAction.Close => "CLOSE",
				_ => "—",
			};

		private static string DecisionClass(SimulationAction? action) =>
			action switch
			{
				SimulationAction.Hold => "action-dist-hold",
				SimulationAction.EnterLong => "action-dist-long",
				SimulationAction.EnterShort => "action-dist-short",
				SimulationAction.Close => "action-dist-close",
				_ => "",
			};

		private int DisplayedWarmupBars => _replayBars is not null ? _replayWarmupBars : SourceEpisodeWarmupBars;
		private SimulationSteppingMode DisplayedSteppingMode =>
			_replayBars is not null ? _replaySteppingMode : SourceEpisodeSteppingMode;
		private double DisplayedCellWidth => _replayBars is not null ? _replayCellWidth : SourceEpisodeCellWidth;

		// Absolute-origin band containing the latest displayed price, same floor(price/width)
		// scheme PriceCellTracker steps decisions on, so it compares against XGrid's own rows.
		private List<XGridLine> DisplayedCellBandLines
		{
			get
			{
				if (
					DisplayedSteppingMode != SimulationSteppingMode.Cell
					|| DisplayedCellWidth <= 0
					|| EpisodeLastPrice is not { } price
				)
					return new List<XGridLine>();

				double cell = Math.Floor(price / DisplayedCellWidth);
				return new List<XGridLine>
				{
					XGridLine.Horizontal(cell * DisplayedCellWidth) with
					{
						Id = "cell-low",
					},
					XGridLine.Horizontal((cell + 1) * DisplayedCellWidth) with
					{
						Id = "cell-high",
					},
				};
			}
		}

		// Index of the last decision whose own timestamp falls within the revealed bars --
		// works for any decisions-per-bar ratio, unlike a fixed "_revealedBarCount - Warmup" offset.
		private int DisplayedDecisionIndex
		{
			get
			{
				var bars = DisplayedEpisodeBars;
				if (bars.Count == 0)
					return -1;
				DateTime cutoff = bars[^1].TimeUtc;
				return DisplayedBarActionsSource.Count(a => a.Timestamp <= cutoff) - 1;
			}
		}

		private double? DisplayedEquity
		{
			get
			{
				if (_replayBars is null)
				{
					var latest = SourceEpisodeBarEquity;
					return latest.Count > 0 ? latest[^1] : null;
				}
				if (_replayBarEquity is null)
					return null;
				int i = DisplayedDecisionIndex;
				return i >= 0 && i < _replayBarEquity.Count ? _replayBarEquity[i] : null;
			}
		}

		// Equity and DisplayedBarActionsSource are appended per decision by RlEpisodeRunner, so
		// index i means the same decision in both -- pairing them by index keeps replay aligned.
		private List<EquityCurveSample> DisplayedEquityCurveSamples
		{
			get
			{
				var actions = DisplayedBarActionsSource;
				var equity = _replayBars is not null ? _replayBarEquity ?? new List<double>() : SourceEpisodeBarEquity;
				int count = _replayBars is not null ? Math.Min(DisplayedDecisionIndex + 1, equity.Count) : equity.Count;
				if (count <= 0)
					return new List<EquityCurveSample>();

				var timestamps = actions.Take(count).Select(a => a.Timestamp).ToList();
				return EquityCurveHelper.Build(timestamps, equity.Take(count).ToList(), DisplayedRevealedTrades);
			}
		}

		private List<ChartVerticalLine> DisplayedWarmupBoundary
		{
			get
			{
				var bars = DisplayedEpisodeBars;
				int warmupBars = DisplayedWarmupBars;
				return warmupBars > 0 && warmupBars < bars.Count
					? [new ChartVerticalLine(bars[warmupBars].TimeUtc, Label: "decisions start")]
					: [];
			}
		}

		private IReadOnlyList<RlBarAction> DisplayedBarActionsSource =>
			_replayBars is not null ? (_replayBarActions ?? new List<RlBarAction>()) : SourceEpisodeBarActions;

		private static (string Shape, string ColorGroup, string Text)? ActionMarkerStyle(RlBarAction barAction)
		{
			if (barAction.Blocked)
			{
				return barAction.Action switch
				{
					SimulationAction.EnterLong => ("circle", "win", "✗L"),
					SimulationAction.EnterShort => ("circle", "loss", "✗S"),
					SimulationAction.Close => ("circle", "shapes", "✗C"),
					_ => null,
				};
			}

			return barAction.Action switch
			{
				SimulationAction.EnterLong => ("arrowUp", "win", "L"),
				SimulationAction.EnterShort => ("arrowDown", "loss", "S"),
				SimulationAction.Close => ("circle", "charts", "C"),
				_ => null,
			};
		}

		private static (string Shape, string ColorGroup, string Text)? CloseReasonMarkerStyle(
			TradeCloseReason reason
		) =>
			reason switch
			{
				TradeCloseReason.StopLoss => ("square", "loss", "SL"),
				TradeCloseReason.TakeProfit => ("square", "win", "TP"),
				_ => null,
			};

		private static DateTime? BarTimeContaining(IReadOnlyList<CTraderBarRow> bars, DateTime timeUtc)
		{
			DateTime? match = null;
			foreach (var b in bars)
			{
				if (b.TimeUtc > timeUtc)
					break;
				match = b.TimeUtc;
			}
			return match;
		}

		private List<ChartMarker> DisplayedActionMarkers
		{
			get
			{
				var bars = DisplayedEpisodeBars;
				var actions = DisplayedBarActionsSource;

				var markers = new List<ChartMarker>();
				if (actions.Count > 0 && bars.Count > 0)
				{
					DateTime cutoff = bars[^1].TimeUtc;
					foreach (var action in actions)
					{
						if (action.Timestamp > cutoff)
							break;
						if (ActionMarkerStyle(action) is not { } style)
							continue;
						markers.Add(new ChartMarker(action.Timestamp, style.Shape, style.ColorGroup, style.Text));
					}
				}

				foreach (var trade in DisplayedRevealedTrades)
				{
					if (CloseReasonMarkerStyle(trade.CloseReason) is not { } style)
						continue;
					if (BarTimeContaining(bars, trade.ExitTime) is not { } barTime)
						continue;
					markers.Add(new ChartMarker(barTime, style.Shape, style.ColorGroup, style.Text));
				}

				return markers;
			}
		}

		private RlBarAction? DisplayedBarAction
		{
			get
			{
				if (_replayBars is null)
				{
					var actions = SourceEpisodeBarActions;
					return actions.Count > 0 ? actions[^1] : null;
				}
				if (_replayBarActions is null)
					return null;
				int i = DisplayedDecisionIndex;
				return i >= 0 && i < _replayBarActions.Count ? _replayBarActions[i] : null;
			}
		}

		private IReadOnlyList<RlInputFeature>? DisplayedBarInputs
		{
			get
			{
				if (_replayBars is null)
				{
					var inputs = SourceEpisodeBarInputs;
					return inputs.Count > 0 ? inputs[^1] : null;
				}
				if (_replayBarInputs is null)
					return null;
				int i = DisplayedDecisionIndex;
				return i >= 0 && i < _replayBarInputs.Count ? _replayBarInputs[i] : null;
			}
		}

		private sealed record InputGroup(
			string Source,
			string ColorClass,
			IReadOnlyList<(string Label, string Value)> Values
		);

		private static string EncoderSourceLabel(EncoderKind kind) =>
			kind switch
			{
				EncoderKind.BarWindow => "Bar Window",
				EncoderKind.VolumeProfile => "Volume Profile",
				EncoderKind.TimeProfile => "Time Profile",
				EncoderKind.SessionClock => "Session Clock",
				EncoderKind.SpreadVolatility => "Spread + Volatility",
				EncoderKind.TraderState => "Trader State",
				EncoderKind.XGrid => "XGrid Signals",
				_ => kind.ToString(),
			};

		private static string InputSourceColorClass(string source) =>
			source switch
			{
				"Bar Window" => "train-inputs-card-1",
				"Volume Profile" => "train-inputs-card-2",
				"Time Profile" => "train-inputs-card-3",
				"Session Clock" => "train-inputs-card-4",
				"Spread + Volatility" => "train-inputs-card-5",
				_ => "train-inputs-card-neutral",
			};

		private List<InputGroup> DisplayedInputGroups
		{
			get
			{
				var recipe = Recipe;
				var inputs = DisplayedBarInputs;
				if (recipe is null || inputs is null || inputs.Count == 0)
					return [];

				var sizes = EncoderSizes;
				var groups = new List<InputGroup>(recipe.EncoderKinds.Count);
				int offset = 0;
				foreach (var kind in recipe.EncoderKinds)
				{
					int count = BrainCatalog.CreateEncoder(kind, sizes).FeatureCount;
					if (offset + count > inputs.Count)
						break;

					var values = inputs
						.Skip(offset)
						.Take(count)
						.Select(f => (f.Label, f.Normalizer.Format(f.Value)))
						.ToList();
					var source = EncoderSourceLabel(kind);
					groups.Add(new InputGroup(source, InputSourceColorClass(source), values));
					offset += count;
				}
				return groups;
			}
		}

		private string NetworkInputsEmptyReason
		{
			get
			{
				var recipe = Recipe;
				if (recipe is null)
					return $"no recipe found for '{BrainName}' -- built-in/fixed brains, and brains never saved as a network, carry no encoder breakdown";
				var inputs = DisplayedBarInputs;
				if (inputs is null)
					return "no decision has been made yet at the currently displayed bar";
				if (inputs.Count == 0)
					return "the network's recorded input vector is empty";
				return $"recipe encoders ({string.Join("+", recipe.EncoderKinds)}) don't add up to the recorded {inputs.Count} inputs -- rebuild the brain to resync";
			}
		}

		private double? DisplayedEntryPrice => DisplayedBarAction?.EntryPrice;
		private bool? DisplayedPositionIsLong => DisplayedBarAction?.PositionIsLong;
		private double? DisplayedStopLossPrice => DisplayedBarAction?.StopLossPrice;
		private double? DisplayedTakeProfitPrice => DisplayedBarAction?.TakeProfitPrice;

		private int DisplayedWindowBarCount => BrainService.GetWarmupBars(BrainName);

		private List<ChartRangeFill> DisplayedWindowRange
		{
			get
			{
				var bars = DisplayedEpisodeBars;
				int windowSize = DisplayedWindowBarCount;
				if (bars.Count == 0 || windowSize <= 0)
					return [];

				int endIndex = bars.Count - 1;
				int startIndex = Math.Max(0, endIndex - windowSize + 1);
				return [new(bars[startIndex].TimeUtc, bars[endIndex].TimeUtc, ColorGroup: "profile")];
			}
		}

		private void PlayEpisode()
		{
			if (ReplayHasPausedProgress)
			{
				_isReplaying = true;
				ScheduleNextReveal();
				return;
			}

			if (!CanReplay)
				return;
			_replayBars = EpisodeBarRows;
			_replayTrades = SourceEpisodeTrades.ToList();
			_replayBarEquity = SourceEpisodeBarEquity.ToList();
			_replayBarActions = SourceEpisodeBarActions.ToList();
			_replayRawBars = SourceEpisodeBars.ToList();
			_replayHistograms = SourceEpisodeBarHistograms.ToList();
			_replayBarInputs = SourceEpisodeBarInputs.ToList();
			_replayWarmupBars = SourceEpisodeWarmupBars;
			_replaySteppingMode = SourceEpisodeSteppingMode;
			_replayCellWidth = SourceEpisodeCellWidth;
			_revealedBarCount = 0;
			_isReplaying = true;
			ScheduleNextReveal();
		}

		private void PauseEpisode()
		{
			_isReplaying = false;
			_replayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
		}

		private void StopAndResetReplay()
		{
			_isReplaying = false;
			_replayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
			_replayBars = null;
			_replayTrades = null;
			_replayBarEquity = null;
			_replayBarActions = null;
			_replayRawBars = null;
			_replayHistograms = null;
			_replayBarInputs = null;
			_replayWarmupBars = 0;
			_replaySteppingMode = SimulationSteppingMode.Bar;
			_replayCellWidth = 0;
			_revealedBarCount = 0;
		}

		private void ScheduleNextReveal()
		{
			_replayTimer ??= new Timer(_ => RevealNextBar());
			_replayTimer.Change(ReplayIntervalMs, Timeout.Infinite);
		}

		private void RevealNextBar()
		{
			if (_replayBars is null)
				return;

			_revealedBarCount = Math.Min(_replayBars.Count, _revealedBarCount + 1);
			if (_revealedBarCount >= _replayBars.Count)
				_isReplaying = false;
			else if (_isReplaying)
				ScheduleNextReveal();

			_ = InvokeAsync(StateHasChanged);
		}

		private void DecreaseReplaySpeed() => _replaySpeedIndex = Math.Max(0, _replaySpeedIndex - 1);

		private void IncreaseReplaySpeed() =>
			_replaySpeedIndex = Math.Min(ReplaySpeedOptions.Length - 1, _replaySpeedIndex + 1);

		private static string FormatReplaySpeed(double v) =>
			(v < 1 ? v.ToString("0.##", CultureInfo.InvariantCulture) : v.ToString("0", CultureInfo.InvariantCulture))
			+ "x";

		private int ProgressPercent =>
			Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);

		private double WinRatePercent
		{
			get
			{
				var episodes = Service.RecentEpisodes;
				return episodes.Count == 0 ? 0 : episodes.Count(e => e.Reward > 0) * 100.0 / episodes.Count;
			}
		}

		private double RewardSpread => Service.BestReward - Service.WorstReward;

		private (double StdDev, double Median) RewardDistribution
		{
			get
			{
				var episodes = Service.RecentEpisodes;
				return episodes.Count == 0
					? (0, 0)
					: (RlEpisodeMetricStats.StdDev(episodes), RlEpisodeMetricStats.Median(episodes));
			}
		}

		private (double Hold, double EnterLong, double EnterShort, double Close) ActionPercents
		{
			get
			{
				var counts = SourceActionCounts;
				int total = Math.Max(1, counts.Total);
				return (
					counts.Hold * 100.0 / total,
					counts.EnterLong * 100.0 / total,
					counts.EnterShort * 100.0 / total,
					counts.Close * 100.0 / total
				);
			}
		}

		private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

		private enum StatsTab
		{
			Session,
			Trader,
		}

		private StatsTab _activeStatsTab;

		private sealed record MetricsRow(int Episode, double Reward, double FinalEquity);

		private List<MetricsRow> MetricsRows =>
			SourceChartEpisodes
				.Select(m => new MetricsRow(m.EpisodeNumber, m.Reward, m.FinalEquity))
				.Reverse()
				.ToList();

		private static string MetricsCellClass(MetricsRow item, string propName) =>
			propName == nameof(MetricsRow.Reward) ? DataTablePnlClass(item.Reward) : "";

		private sealed record TraderStatsRow(
			string Episode,
			int Won,
			int Lost,
			double BiggestWin,
			double BiggestLoss,
			double AverageWin,
			double AverageLoss,
			int Buys,
			int Sells,
			string TimeInMarket,
			string TimeInLong,
			string TimeInShort,
			double MaxEquity,
			double MaxDrawdown,
			int LongestWinStreak,
			int LongestLossStreak,
			int SlHits,
			double SlEquity,
			int TpHits,
			double TpEquity,
			int BlockedLongs,
			int BlockedShorts,
			int BlockedCloses
		);

		private static string TimeSpanStr(TimeSpan ts) => ts.ToString(@"d\.hh\:mm\:ss");

		private static TraderStatsRow ToTraderStatsRow(
			string episodeLabel,
			TradeStatistics s,
			int blockedLongs,
			int blockedShorts,
			int blockedCloses
		) =>
			new(
				episodeLabel,
				s.WinCount,
				s.LossCount,
				s.BiggestWin,
				s.BiggestLoss,
				s.AverageWin,
				s.AverageLoss,
				s.BuyCount,
				s.SellCount,
				TimeSpanStr(s.TimeInMarket),
				TimeSpanStr(s.TimeInLong),
				TimeSpanStr(s.TimeInShort),
				s.MaxEquity,
				s.MaxDrawdown,
				s.LongestWinStreak,
				s.LongestLossStreak,
				s.StopLossHitCount,
				s.StopLossPnl,
				s.TakeProfitHitCount,
				s.TakeProfitPnl,
				blockedLongs,
				blockedShorts,
				blockedCloses
			);

		private static string TraderStatsCellClass(TraderStatsRow item, string propName) =>
			propName switch
			{
				nameof(TraderStatsRow.BiggestWin) => DataTablePnlClass(item.BiggestWin),
				nameof(TraderStatsRow.AverageWin) => DataTablePnlClass(item.AverageWin),
				nameof(TraderStatsRow.BiggestLoss) => DataTablePnlClass(item.BiggestLoss),
				nameof(TraderStatsRow.AverageLoss) => DataTablePnlClass(item.AverageLoss),
				nameof(TraderStatsRow.Won) => item.Won > 0 ? "data-table-value-positive" : "",
				nameof(TraderStatsRow.Lost) => item.Lost > 0 ? "data-table-value-negative" : "",
				nameof(TraderStatsRow.MaxDrawdown) => item.MaxDrawdown > 0 ? "data-table-value-negative" : "",
				nameof(TraderStatsRow.LongestWinStreak) => item.LongestWinStreak > 0 ? "data-table-value-positive" : "",
				nameof(TraderStatsRow.LongestLossStreak) => item.LongestLossStreak > 0
					? "data-table-value-negative"
					: "",
				nameof(TraderStatsRow.SlEquity) => DataTablePnlClass(item.SlEquity),
				nameof(TraderStatsRow.TpEquity) => DataTablePnlClass(item.TpEquity),
				nameof(TraderStatsRow.BlockedLongs) => item.BlockedLongs > 0 ? "data-table-value-negative" : "",
				nameof(TraderStatsRow.BlockedShorts) => item.BlockedShorts > 0 ? "data-table-value-negative" : "",
				nameof(TraderStatsRow.BlockedCloses) => item.BlockedCloses > 0 ? "data-table-value-negative" : "",
				_ => "",
			};

		private static string DataTablePnlClass(double v) =>
			v switch
			{
				> 0 => "data-table-value-positive",
				< 0 => "data-table-value-negative",
				_ => "",
			};

		private List<TraderStatsRow> EpisodeTraderStatsRows =>
			SourceRecentEpisodeTrades
				.Select(e =>
					ToTraderStatsRow(
						e.EpisodeNumber.ToString(CultureInfo.InvariantCulture),
						TradeStatistics.From(e.Trades, e.MaxEquity, e.MaxDrawdown),
						e.BlockedLongs,
						e.BlockedShorts,
						e.BlockedCloses
					)
				)
				.Reverse()
				.ToList();

		private List<TraderStatsRow> SessionTraderStatsRows
		{
			get
			{
				var episodes = SourceRecentEpisodeTrades;
				if (episodes.Count == 0)
					return new List<TraderStatsRow>();

				var trades = episodes.SelectMany(e => e.Trades).ToList();
				var stats = TradeStatistics.From(
					trades,
					episodes.Max(e => e.MaxEquity),
					episodes.Max(e => e.MaxDrawdown)
				);
				return new List<TraderStatsRow>
				{
					ToTraderStatsRow(
						$"{episodes.Count} episodes",
						stats,
						episodes.Sum(e => e.BlockedLongs),
						episodes.Sum(e => e.BlockedShorts),
						episodes.Sum(e => e.BlockedCloses)
					),
				};
			}
		}
	}
}
