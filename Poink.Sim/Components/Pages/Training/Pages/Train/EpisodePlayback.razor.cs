using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Training;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Training
{
    public partial class EpisodePlayback : ComponentBase, IDisposable
    {
        [Inject] private IRlTrainingService Service { get; set; } = default!;

        [Parameter] public string BrainName { get; set; } = "";

        private BrainRecipe? Recipe => Service.GetRecipe(BrainName);

        private bool _trainingWasAdvancing;

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

            _ = InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            Service.OnChanged -= HandleServiceChanged;
            _replayTimer?.Dispose();
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
            IReadOnlyList<Trade> LatestEpisodeTrades,
            IReadOnlyList<double> LatestEpisodeBarEquity,
            IReadOnlyList<RlBarAction> LatestEpisodeBarActions,
            IReadOnlyList<IReadOnlyList<RlInputFeature>> LatestEpisodeBarInputs);

        private void ToggleLiveFeedback()
        {
            if (_liveFeedbackPaused)
            {
                _liveFeedbackPaused = false;
                _liveFeedbackSnapshot = null;
                return;
            }

            _liveFeedbackSnapshot = new LiveFeedbackSnapshot(
                Service.RecentEpisodes, Service.RecentEpisodeTrades, Service.ActionCounts,
                Service.LatestEpisodeBars, Service.LatestEpisodeBarHistograms, Service.LatestEpisodeWarmupBars,
                Service.LatestEpisodeTrades, Service.LatestEpisodeBarEquity, Service.LatestEpisodeBarActions,
                Service.LatestEpisodeBarInputs);
            _liveFeedbackPaused = true;
        }

        private bool SourceIsRunning => !_liveFeedbackPaused && Service.IsRunning;
        private IReadOnlyList<RlEpisodeMetric> SourceChartEpisodes => _liveFeedbackSnapshot?.RecentEpisodes ?? Service.RecentEpisodes;
        private IReadOnlyList<RlEpisodeTrades> SourceRecentEpisodeTrades => _liveFeedbackSnapshot?.RecentEpisodeTrades ?? Service.RecentEpisodeTrades;
        private RlActionCounts SourceActionCounts => _liveFeedbackSnapshot?.ActionCounts ?? Service.ActionCounts;
        private IReadOnlyList<HlocAvdtBar> SourceEpisodeBars => _liveFeedbackSnapshot?.LatestEpisodeBars ?? Service.LatestEpisodeBars;
        private IReadOnlyList<BarPriceHistogram> SourceEpisodeBarHistograms => _liveFeedbackSnapshot?.LatestEpisodeBarHistograms ?? Service.LatestEpisodeBarHistograms;
        private int SourceEpisodeWarmupBars => _liveFeedbackSnapshot?.LatestEpisodeWarmupBars ?? Service.LatestEpisodeWarmupBars;
        private IReadOnlyList<Trade> SourceEpisodeTrades => _liveFeedbackSnapshot?.LatestEpisodeTrades ?? Service.LatestEpisodeTrades;
        private IReadOnlyList<double> SourceEpisodeBarEquity => _liveFeedbackSnapshot?.LatestEpisodeBarEquity ?? Service.LatestEpisodeBarEquity;
        private IReadOnlyList<RlBarAction> SourceEpisodeBarActions => _liveFeedbackSnapshot?.LatestEpisodeBarActions ?? Service.LatestEpisodeBarActions;
        private IReadOnlyList<IReadOnlyList<RlInputFeature>> SourceEpisodeBarInputs => _liveFeedbackSnapshot?.LatestEpisodeBarInputs ?? Service.LatestEpisodeBarInputs;

        private List<CTraderBarRow> EpisodeBarRows =>
            SourceEpisodeBars
                .Select(b => new CTraderBarRow(b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance))
                .ToList();

        private IReadOnlyList<HlocAvdtBar> DisplayedEpisodeBarsRaw =>
            _replayRawBars is not null ? _replayRawBars.Take(_revealedBarCount).ToList() : SourceEpisodeBars;

        private IReadOnlyList<BarPriceHistogram> DisplayedEpisodeBarHistograms =>
            _replayHistograms is not null ? _replayHistograms.Take(_revealedBarCount).ToList() : SourceEpisodeBarHistograms;

        private EncoderSizeOptions EncoderSizes => Recipe?.EncoderSizes ?? new EncoderSizeOptions();

        private double? EpisodeLastPrice =>
            DisplayedEpisodeBarsRaw.Count > 0 ? DisplayedEpisodeBarsRaw[^1].Close : null;

        private List<WeightedPriceSample> EpisodeVolumeProfileSamples
        {
            get
            {
                var histograms = DisplayedEpisodeBarHistograms;
                int window = EncoderSizes.VolumeProfileLookbackBars > 0 ? EncoderSizes.VolumeProfileLookbackBars : histograms.Count;
                return BarPriceHistogram.Merge(histograms, window)
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
                int window = EncoderSizes.TimeProfileLookbackBars > 0 ? EncoderSizes.TimeProfileLookbackBars : histograms.Count;
                return BarPriceHistogram.MergeTime(bars, histograms, window)
                    .Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
                    .ToList();
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
        private int _revealedBarCount;
        private bool _isReplaying;
        private Timer? _replayTimer;

        private double ReplaySpeed => ReplaySpeedOptions[Math.Clamp(_replaySpeedIndex, 0, ReplaySpeedOptions.Length - 1)];
        private int ReplayIntervalMs => Math.Max(10, (int)(1000.0 / (BaseBarsPerSecond * ReplaySpeed)));

        private bool CanReplay => EpisodeBarRows.Count > 0;

        private bool ReplayHasPausedProgress => _replayBars is not null && _revealedBarCount > 0 && _revealedBarCount < _replayBars.Count;

        private List<CTraderBarRow> DisplayedEpisodeBars =>
            _replayBars is not null ? _replayBars.Take(_revealedBarCount).ToList() : EpisodeBarRows;

        private List<Trade> DisplayedRevealedTrades
        {
            get
            {
                var trades = _replayBars is not null ? _replayTrades ?? new List<Trade>() : SourceEpisodeTrades;
                if (trades.Count == 0)
                    return new List<Trade>();

                var visibleUntil = _replayBars is null
                    ? DateTime.MaxValue
                    : _revealedBarCount > 0 ? _replayBars[_revealedBarCount - 1].TimeUtc : DateTime.MinValue;

                return trades.Where(t => t.ExitTime <= visibleUntil).ToList();
            }
        }

        private List<ChartDiagonalLine> DisplayedTradeLines =>
            DisplayedRevealedTrades
                .Select(t => new ChartDiagonalLine(
                    t.EntryTime, t.EntryPrice, t.ExitTime, t.ExitPrice, ColorGroup: t.RealizedPnl >= 0 ? "win" : "loss"))
                .ToList();

        private TradeStatistics DisplayedTraderStats => TradeStatistics.From(DisplayedRevealedTrades, 0, 0);

        private (int Longs, int Shorts, int Closes) DisplayedBlockedCounts
        {
            get
            {
                var bars = DisplayedEpisodeBars;
                var actions = DisplayedBarActionsSource;
                int warmupBars = DisplayedWarmupBars;
                if (actions.Count == 0 || bars.Count <= warmupBars)
                    return (0, 0, 0);

                int revealedDecisions = Math.Min(actions.Count, bars.Count - warmupBars);
                int longs = 0, shorts = 0, closes = 0;
                for (int i = 0; i < revealedDecisions; i++)
                {
                    if (!actions[i].Blocked)
                        continue;
                    if (actions[i].Action == SimulationAction.EnterLong)
                        longs++;
                    else if (actions[i].Action == SimulationAction.EnterShort)
                        shorts++;
                    else if (actions[i].Action == SimulationAction.Close)
                        closes++;
                }
                return (longs, shorts, closes);
            }
        }

        private static string DecisionLabel(SimulationAction? action) => action switch
        {
            SimulationAction.Hold => "HOLD",
            SimulationAction.EnterLong => "LONG",
            SimulationAction.EnterShort => "SHORT",
            SimulationAction.Close => "CLOSE",
            _ => "—",
        };

        private static string DecisionClass(SimulationAction? action) => action switch
        {
            SimulationAction.Hold => "action-dist-hold",
            SimulationAction.EnterLong => "action-dist-long",
            SimulationAction.EnterShort => "action-dist-short",
            SimulationAction.Close => "action-dist-close",
            _ => "",
        };

        private int DisplayedWarmupBars => _replayBars is not null ? _replayWarmupBars : SourceEpisodeWarmupBars;

        private int DisplayedDecisionIndex => _revealedBarCount - 1 - DisplayedWarmupBars;

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

        private static (string Shape, string ColorGroup, string Text)? CloseReasonMarkerStyle(TradeCloseReason reason) => reason switch
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
                int warmupBars = DisplayedWarmupBars;

                var markers = new List<ChartMarker>();
                if (actions.Count > 0 && bars.Count > warmupBars)
                {
                    for (int i = warmupBars; i < bars.Count; i++)
                    {
                        int decisionIndex = i - warmupBars;
                        if (decisionIndex >= actions.Count)
                            break;
                        if (ActionMarkerStyle(actions[decisionIndex]) is not { } style)
                            continue;
                        markers.Add(new ChartMarker(bars[i].TimeUtc, style.Shape, style.ColorGroup, style.Text));
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

        private sealed record InputGroup(string Source, string ColorClass, IReadOnlyList<(string Label, string Value)> Values);

        private static string EncoderSourceLabel(EncoderKind kind) => kind switch
        {
            EncoderKind.BarWindow => "Bar Window",
            EncoderKind.VolumeProfile => "Volume Profile",
            EncoderKind.TimeProfile => "Time Profile",
            EncoderKind.SessionClock => "Session Clock",
            EncoderKind.SpreadVolatility => "Spread + Volatility",
            EncoderKind.TraderState => "Trader State",
            _ => kind.ToString(),
        };

        private static string InputSourceColorClass(string source) => source switch
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

                    var values = inputs.Skip(offset).Take(count)
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

        private int DisplayedWindowBarCount => Service.GetWarmupBars(BrainName);

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

        private void IncreaseReplaySpeed() => _replaySpeedIndex = Math.Min(ReplaySpeedOptions.Length - 1, _replaySpeedIndex + 1);

        private static string FormatReplaySpeed(double v) =>
            (v < 1 ? v.ToString("0.##", CultureInfo.InvariantCulture) : v.ToString("0", CultureInfo.InvariantCulture)) + "x";

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
                return (counts.Hold * 100.0 / total, counts.EnterLong * 100.0 / total,
                        counts.EnterShort * 100.0 / total, counts.Close * 100.0 / total);
            }
        }

        private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private enum StatsTab { Session, Trader }
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
            string Episode, int Won, int Lost, double BiggestWin, double BiggestLoss,
            double AverageWin, double AverageLoss, int Buys, int Sells,
            string TimeInMarket, string TimeInLong, string TimeInShort,
            double MaxEquity, double MaxDrawdown, int LongestWinStreak, int LongestLossStreak,
            int SlHits, double SlEquity, int TpHits, double TpEquity,
            int BlockedLongs, int BlockedShorts, int BlockedCloses);

        private static string TimeSpanStr(TimeSpan ts) => ts.ToString(@"d\.hh\:mm\:ss");

        private static TraderStatsRow ToTraderStatsRow(
            string episodeLabel, TradeStatistics s, int blockedLongs, int blockedShorts, int blockedCloses) => new(
            episodeLabel, s.WinCount, s.LossCount, s.BiggestWin, s.BiggestLoss,
            s.AverageWin, s.AverageLoss, s.BuyCount, s.SellCount,
            TimeSpanStr(s.TimeInMarket), TimeSpanStr(s.TimeInLong), TimeSpanStr(s.TimeInShort),
            s.MaxEquity, s.MaxDrawdown, s.LongestWinStreak, s.LongestLossStreak,
            s.StopLossHitCount, s.StopLossPnl, s.TakeProfitHitCount, s.TakeProfitPnl,
            blockedLongs, blockedShorts, blockedCloses);

        private static string TraderStatsCellClass(TraderStatsRow item, string propName) => propName switch
        {
            nameof(TraderStatsRow.BiggestWin) => DataTablePnlClass(item.BiggestWin),
            nameof(TraderStatsRow.AverageWin) => DataTablePnlClass(item.AverageWin),
            nameof(TraderStatsRow.BiggestLoss) => DataTablePnlClass(item.BiggestLoss),
            nameof(TraderStatsRow.AverageLoss) => DataTablePnlClass(item.AverageLoss),
            nameof(TraderStatsRow.Won) => item.Won > 0 ? "data-table-value-positive" : "",
            nameof(TraderStatsRow.Lost) => item.Lost > 0 ? "data-table-value-negative" : "",
            nameof(TraderStatsRow.MaxDrawdown) => item.MaxDrawdown > 0 ? "data-table-value-negative" : "",
            nameof(TraderStatsRow.LongestWinStreak) => item.LongestWinStreak > 0 ? "data-table-value-positive" : "",
            nameof(TraderStatsRow.LongestLossStreak) => item.LongestLossStreak > 0 ? "data-table-value-negative" : "",
            nameof(TraderStatsRow.SlEquity) => DataTablePnlClass(item.SlEquity),
            nameof(TraderStatsRow.TpEquity) => DataTablePnlClass(item.TpEquity),
            nameof(TraderStatsRow.BlockedLongs) => item.BlockedLongs > 0 ? "data-table-value-negative" : "",
            nameof(TraderStatsRow.BlockedShorts) => item.BlockedShorts > 0 ? "data-table-value-negative" : "",
            nameof(TraderStatsRow.BlockedCloses) => item.BlockedCloses > 0 ? "data-table-value-negative" : "",
            _ => "",
        };

        private static string DataTablePnlClass(double v) => v switch
        {
            > 0 => "data-table-value-positive",
            < 0 => "data-table-value-negative",
            _ => "",
        };

        private List<TraderStatsRow> EpisodeTraderStatsRows =>
            SourceRecentEpisodeTrades
                .Select(e => ToTraderStatsRow(
                    e.EpisodeNumber.ToString(CultureInfo.InvariantCulture),
                    TradeStatistics.From(e.Trades, e.MaxEquity, e.MaxDrawdown), e.BlockedLongs, e.BlockedShorts, e.BlockedCloses))
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
                var stats = TradeStatistics.From(trades, episodes.Max(e => e.MaxEquity), episodes.Max(e => e.MaxDrawdown));
                return new List<TraderStatsRow>
                {
                    ToTraderStatsRow($"{episodes.Count} episodes", stats,
                        episodes.Sum(e => e.BlockedLongs), episodes.Sum(e => e.BlockedShorts), episodes.Sum(e => e.BlockedCloses)),
                };
            }
        }
    }
}
