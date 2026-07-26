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
    // Split out of TrainPage so this content owns its own re-render cycle
    // rather than piggybacking on the sticky topbar's. BrainName is the one
    // piece of TrainPage's run-parameter state this side needs, to visualize
    // whichever brain is currently picked there.
    public partial class TrainLiveTab : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        [Parameter] public string BrainName { get; set; } = "";

        // Tracks whether training was actively advancing (running and not
        // paused) as of the last OnChanged -- lets HandleServiceChanged detect
        // the moment it starts advancing again (Start/Resume/Step all flip this
        // false-\>true, if only briefly for Step) versus every subsequent
        // episode of an already-ongoing run, which shouldn't keep re-triggering
        // the same reset. Seeded from current state so mounting mid-run doesn't
        // spuriously fire one.
        private bool _trainingWasAdvancing;

        protected override void OnInitialized()
        {
            _trainingWasAdvancing = Service.IsRunning && !Service.IsPaused;
            Service.OnChanged += HandleServiceChanged;
        }

        private void HandleServiceChanged()
        {
            // Carrying on with training (Start/Resume/Step) makes whatever
            // episode replay was frozen for inspection stale -- stop and reset
            // it back to just following the live episode, rather than leaving
            // it paused on data that's about to be superseded.
            bool isAdvancing = Service.IsRunning && !Service.IsPaused;
            if (isAdvancing && !_trainingWasAdvancing) StopAndResetReplay();
            _trainingWasAdvancing = isAdvancing;

            _ = InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            Service.OnChanged -= HandleServiceChanged;
            _replayTimer?.Dispose();
        }

        // Drives the Live tab's Network Activity animation -- the currently
        // selected brain's real architecture (see IPolicyGradientBrain.
        // DescribeNetworkShape), not a hardcoded shape.
        private IReadOnlyList<NetworkLayerShape> CurrentNetworkShape => Service.DescribeNetworkShape(BrainName);

        // ── Current episode: bar chart + volume/price profiles ──
        // Same mapping SimulationPage.ToBarRows uses for its live OhlcvChart --
        // HlocAvdtBar is internal, so a public [Parameter] can't take it directly.
        private List<CTraderBarRow> EpisodeBarRows =>
            Service.LatestEpisodeBars
                .Select(b => new CTraderBarRow(b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance))
                .ToList();

        // Highlights the episode's last close as "current price" in both profile
        // charts below -- there's no live Bid/Ask once an episode has finished.
        private double? EpisodeLastPrice =>
            Service.LatestEpisodeBars.Count > 0 ? Service.LatestEpisodeBars[^1].Close : null;

        // Merges every bar this episode completed into one profile -- passing the
        // full histogram count as the window merges the whole episode, not just a
        // recent slice (see SimulationPage.MergedBarSamples for the live-sim
        // counterpart, which instead uses a user-adjustable window).
        private List<WeightedPriceSample> EpisodeVolumeProfileSamples =>
            BarPriceHistogram.Merge(Service.LatestEpisodeBarHistograms, Service.LatestEpisodeBarHistograms.Count)
                .Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
                .ToList();

        private List<WeightedTimeSample> EpisodeTimeProfileSamples =>
            BarPriceHistogram.MergeTime(Service.LatestEpisodeBars, Service.LatestEpisodeBarHistograms, Service.LatestEpisodeBarHistograms.Count)
                .Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
                .ToList();

        // ── Episode replay: reveal the current episode's bars/trades one at a
        // time at a user-controlled speed, instead of the finished chart all at
        // once, so watching it reads like "the trader trading" rather than a
        // static result. Purely client-side (a local System.Threading.Timer,
        // same self-rescheduling pattern SimulationEngine's own Timer uses) over
        // a snapshot frozen at Play, so the still-completing episode that
        // triggered Play can't itself repaint mid-animation (see
        // DisplayedEpisodeBars below) -- but the moment training is told to
        // carry on again (Start/Resume/Step), HandleServiceChanged drops that
        // snapshot via StopAndResetReplay so the panel doesn't linger on a
        // now-stale episode.
        private static readonly double[] ReplaySpeedOptions = { 0.25, 0.5, 1, 2, 5, 10, 25 };

        // Bars revealed per second at 1x -- not tied to the episode's own bar
        // width/real elapsed time (which can vary wildly, e.g. weekend gaps),
        // since a bounded ~100-300 bar episode reads better at a flat pace than
        // one time-scaled to real market time.
        private const double BaseBarsPerSecond = 2.0;

        private int _replaySpeedIndex = 2; // 1x
        private List<CTraderBarRow>? _replayBars;
        private List<Trade>? _replayTrades;
        private List<IReadOnlyList<RlInputFeature>>? _replayBarInputs;
        private List<double>? _replayBarEquity;
        private List<IReadOnlyList<RlOutputFeature>>? _replayBarOutputs;
        private int _revealedBarCount;
        private bool _isReplaying;
        private Timer? _replayTimer;

        private double ReplaySpeed => ReplaySpeedOptions[Math.Clamp(_replaySpeedIndex, 0, ReplaySpeedOptions.Length - 1)];
        private int ReplayIntervalMs => Math.Max(10, (int)(1000.0 / (BaseBarsPerSecond * ReplaySpeed)));

        private bool CanReplay => EpisodeBarRows.Count > 0;

        // True once Play has been pressed and paused (or the animation is mid-
        // flight) without yet reaching the end -- drives the PLAY/RESUME label.
        private bool ReplayHasPausedProgress => _replayBars is not null && _revealedBarCount > 0 && _revealedBarCount < _replayBars.Count;

        // The chart's own Bars -- the live current episode while idle/finished,
        // or the frozen snapshot revealed up to _revealedBarCount while a replay
        // is in progress or paused mid-way.
        private List<CTraderBarRow> DisplayedEpisodeBars =>
            _replayBars is not null ? _replayBars.Take(_revealedBarCount).ToList() : EpisodeBarRows;

        // Closed trades whose exit has been "revealed" yet -- entry-to-exit
        // diagonal lines, colored by realized P&L sign (see the win/loss groups
        // added to drawableOhlcvChart.js's groupColors for this).
        private List<ChartDiagonalLine> DisplayedTradeLines
        {
            get
            {
                var trades = _replayBars is not null ? _replayTrades ?? new List<Trade>() : Service.LatestEpisodeTrades;
                if (trades.Count == 0) return new List<ChartDiagonalLine>();

                var visibleUntil = _replayBars is null
                    ? DateTime.MaxValue
                    : _revealedBarCount > 0 ? _replayBars[_revealedBarCount - 1].TimeUtc : DateTime.MinValue;

                return trades
                    .Where(t => t.ExitTime <= visibleUntil)
                    .Select(t => new ChartDiagonalLine(
                        t.EntryTime, t.EntryPrice, t.ExitTime, t.ExitPrice, ColorGroup: t.RealizedPnl >= 0 ? "win" : "loss"))
                    .ToList();
            }
        }

        // What the INPUTS panel below shows -- the episode's final decision
        // while idle, or (while a replay is in progress/paused) whatever the
        // network saw as of the currently-revealed bar, so the panel updates in
        // step with the chart instead of only ever showing the last decision.
        private IReadOnlyList<RlInputFeature>? DisplayedInputs =>
            _replayBars is not null && _replayBarInputs is not null
                && _revealedBarCount > 0 && _revealedBarCount <= _replayBarInputs.Count
                ? _replayBarInputs[_revealedBarCount - 1]
                : Service.LatestInputs;

        // Same "which decision point" pairing as DisplayedInputs, but what the
        // network decided from those inputs -- the OUTPUTS panel's data source.
        private IReadOnlyList<RlOutputFeature>? DisplayedOutputs =>
            _replayBars is not null && _replayBarOutputs is not null
                && _revealedBarCount > 0 && _revealedBarCount <= _replayBarOutputs.Count
                ? _replayBarOutputs[_revealedBarCount - 1]
                : Service.LatestOutputs;

        // Same "which decision point" pairing as DisplayedInputs, but the
        // pre-Apply equity CapitalStateEncoder actually divided by (see
        // RlEpisode.BarEquity's doc comment) -- the anchor the de-normalized
        // toggle needs to invert "unrealized/eq"/"realized/eq"/"cash/eq".
        private double? DisplayedEquity
        {
            get
            {
                if (_replayBars is not null && _replayBarEquity is not null
                    && _revealedBarCount > 0 && _revealedBarCount <= _replayBarEquity.Count)
                    return _replayBarEquity[_revealedBarCount - 1];

                var latest = Service.LatestEpisodeBarEquity;
                return latest.Count > 0 ? latest[^1] : null;
            }
        }

        // The currently-displayed bar's own close -- BarWindowEncoder/
        // VolumeProfileEncoder both compute their price-window/offset features
        // as percent-change-from-current-price, and that "current price" is
        // exactly this bar's Close (see RecordStep's call site in
        // RlEpisodeRunner, which passes completed.Value.Bar.Close straight
        // through as SimulationObservation.CurrentPrice). The anchor the
        // de-normalized toggle needs to invert "t-N"/"poc-offset"/etc.
        private double? DisplayedCurrentPrice =>
            DisplayedEpisodeBars.Count > 0 ? DisplayedEpisodeBars[^1].Close : null;

        // How many trailing bars feed the currently-selected brain's decision --
        // the max of a fixed-size price window (BarWindowEncoder; derived from
        // how many "t-N" entries DescribeInputs actually produced, since that
        // encoder's WindowSize is a private constant nothing exposes directly)
        // and a volume-profile lookback (VolumeProfileEncoder, queryable
        // generically via IHasLookbackBars). 0 for a brain with neither (e.g.
        // a heuristic brain, where DisplayedInputs is null to begin with).
        private int DisplayedWindowBarCount => Math.Max(PriceWindowFeatures.Count, Service.GetLookbackBars(BrainName) ?? 0);

        // Shades the trailing DisplayedWindowBarCount bars ending at whichever
        // bar the chart/INPUTS panel currently show -- "what range the trader
        // sees" for that decision. Reuses DrawableOhlcvChart's "profile" color
        // group (computed, not user-drawn, same as this) rather than adding a
        // new one.
        private List<ChartRangeFill> DisplayedWindowRange
        {
            get
            {
                var bars = DisplayedEpisodeBars;
                int windowSize = DisplayedWindowBarCount;
                if (bars.Count == 0 || windowSize <= 0) return [];

                int endIndex = bars.Count - 1;
                int startIndex = Math.Max(0, endIndex - windowSize + 1);
                return [new(bars[startIndex].TimeUtc, bars[endIndex].TimeUtc, ColorGroup: "profile")];
            }
        }

        // Toggles the INPUTS panel between the network's own normalized
        // (bounded ratio) values and their de-normalized real-world units --
        // see FormatInputValue for the per-label inversion.
        private bool _showDenormalizedInputs;

        // Converts one feature's normalized value into a human string. Price-
        // window/offset features (labels "t-N"/"poc-offset"/"va-*-offset")
        // invert via DisplayedCurrentPrice, account-ratio features
        // ("unrealized/eq" etc.) via DisplayedEquity, "vp-N" volume-profile
        // buckets just reformat as a percentage (inverting to a raw tick count
        // would need re-merging BarPriceHistograms, not worth it for a
        // fraction that already reads fine as one), and "position" becomes a
        // direction label. Falls back to the plain normalized value if the
        // needed anchor isn't available yet (e.g. no equity captured because
        // the episode's brain isn't describable).
        private string FormatInputValue(RlInputFeature f, string normalizedFormat = "N4") =>
            !_showDenormalizedInputs ? f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture) : f.Label switch
            {
                "position" => f.Value switch { > 0 => "LONG", < 0 => "SHORT", _ => "FLAT" },
                "unrealized/eq" or "realized/eq" or "cash/eq" => DisplayedEquity is { } equity
                    ? (f.Value * equity).ToString("N2", CultureInfo.InvariantCulture)
                    : f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture),
                _ when f.Label.StartsWith("t-", StringComparison.Ordinal) || f.Label is "poc-offset" or "va-high-offset" or "va-low-offset" =>
                    DisplayedCurrentPrice is { } price
                        ? (price * (1 + f.Value)).ToString("F5", CultureInfo.InvariantCulture)
                        : f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture),
                _ when f.Label.StartsWith("vp-", StringComparison.Ordinal) =>
                    (f.Value * 100).ToString("N1", CultureInfo.InvariantCulture) + "%",
                _ => f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture),
            };

        // PLAY: starts fresh from bar 0 over a new snapshot of whatever episode
        // is currently loaded, unless there's paused progress to continue from
        // instead (see ReplayHasPausedProgress) -- one button doubles as both,
        // same as Simulation's Play/Pause toggle.
        private void PlayEpisode()
        {
            if (ReplayHasPausedProgress)
            {
                _isReplaying = true;
                ScheduleNextReveal();
                return;
            }

            if (!CanReplay) return;
            _replayBars = EpisodeBarRows;
            _replayTrades = Service.LatestEpisodeTrades.ToList();
            _replayBarInputs = Service.LatestEpisodeBarInputs.ToList();
            _replayBarEquity = Service.LatestEpisodeBarEquity.ToList();
            _replayBarOutputs = Service.LatestEpisodeBarOutputs.ToList();
            _revealedBarCount = 0;
            _isReplaying = true;
            ScheduleNextReveal();
        }

        private void PauseEpisode()
        {
            _isReplaying = false;
            _replayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        // Called when training starts advancing again (see HandleServiceChanged)
        // -- drops the frozen snapshot entirely (not just pausing it) so the
        // panel falls back to following Service.LatestEpisodeBars/Trades live
        // once a new episode lands, instead of staying stuck on a now-stale one.
        private void StopAndResetReplay()
        {
            _isReplaying = false;
            _replayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _replayBars = null;
            _replayTrades = null;
            _replayBarInputs = null;
            _replayBarEquity = null;
            _replayBarOutputs = null;
            _revealedBarCount = 0;
        }

        private void ScheduleNextReveal()
        {
            _replayTimer ??= new Timer(_ => RevealNextBar());
            _replayTimer.Change(ReplayIntervalMs, Timeout.Infinite);
        }

        // Timer callback -- runs on a thread-pool thread, not the circuit's sync
        // context, so the actual re-render is marshalled via InvokeAsync
        // (same pattern HandleServiceChanged already uses for RlTrainingService's
        // own background training loop).
        private void RevealNextBar()
        {
            if (_replayBars is null) return;

            _revealedBarCount = Math.Min(_replayBars.Count, _revealedBarCount + 1);
            if (_revealedBarCount >= _replayBars.Count) _isReplaying = false;
            else if (_isReplaying) ScheduleNextReveal();

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

        // Best/worst are already whole-run extremes tracked by the service --
        // define spread from those same two displayed numbers rather than
        // re-deriving max-min from RecentEpisodes below, which is capped to
        // the last 500 and would silently disagree with Best/Worst on a
        // longer run.
        private double RewardSpread => Service.BestReward - Service.WorstReward;

        // Same bounded episode window as WinRatePercent above. Population
        // (not sample) standard deviation -- this describes the actual
        // observed episodes rather than estimating some larger population.
        private (double StdDev, double Median) RewardDistribution
        {
            get
            {
                var rewards = Service.RecentEpisodes.Select(e => e.Reward).ToList();
                if (rewards.Count == 0) return (0, 0);

                double mean = rewards.Average();
                double variance = rewards.Sum(r => (r - mean) * (r - mean)) / rewards.Count;
                double stdDev = Math.Sqrt(variance);

                var sorted = rewards.OrderBy(r => r).ToList();
                int mid = sorted.Count / 2;
                double median = sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];

                return (stdDev, median);
            }
        }

        private (double Hold, double EnterLong, double EnterShort, double Close) ActionPercents
        {
            get
            {
                var counts = Service.ActionCounts;
                int total = Math.Max(1, counts.Total);
                return (counts.Hold * 100.0 / total, counts.EnterLong * 100.0 / total,
                        counts.EnterShort * 100.0 / total, counts.Close * 100.0 / total);
            }
        }

        // CSS width percentages must use '.' as the decimal separator regardless of
        // host locale (this host's is comma-decimal) -- see TickVolumeProfileChart
        // for the same concern.
        private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // One decimal place (unlike Pct's variable precision) -- probabilities
        // are always in [0,100] and this reads better lined up in the OUTPUTS
        // panel's fixed-width column than Pct's "0.##" would.
        private static string FormatProbabilityPct(double probability) => (probability * 100).ToString("N1", CultureInfo.InvariantCulture);

        // Maps an RlOutputFeature.Label (a SimulationAction member name, see
        // ComposedPolicyBrain.ActionLabels) onto the same short display text
        // and action-dist-* CSS suffix the ACTION DISTRIBUTION panel above
        // already uses, so the OUTPUTS panel reads as the same family instead
        // of introducing its own color/label convention.
        private static string ActionShortLabel(string actionLabel) => actionLabel switch
        {
            "Hold" => "HOLD",
            "EnterLong" => "LONG",
            "EnterShort" => "SHORT",
            "Close" => "CLOSE",
            _ => actionLabel,
        };

        private static string ActionCssSuffix(string actionLabel) => actionLabel switch
        {
            "Hold" => "hold",
            "EnterLong" => "long",
            "EnterShort" => "short",
            "Close" => "close",
            _ => "",
        };

        // MlpPolicyBrain.DescribeInputs labels the price-window entries "t-N" and
        // the account-state scalars by name (see that method) -- split on that
        // convention rather than a fixed count, so this keeps working if a future
        // brain's window size differs.
        private List<RlInputFeature> PriceWindowFeatures =>
            DisplayedInputs?.Where(f => f.Label.StartsWith("t-", StringComparison.Ordinal)).ToList() ?? new List<RlInputFeature>();

        private List<RlInputFeature> ScalarFeatures =>
            DisplayedInputs?.Where(f => !f.Label.StartsWith("t-", StringComparison.Ordinal)).ToList() ?? new List<RlInputFeature>();

        // Which of the three DataTable tabs below is showing -- mirrors
        // SimulationPage's LogTab/_activeLogTab pattern for its own tabbed panel.
        private enum StatsTab { Episode, Trader, Session }
        private StatsTab _activeStatsTab;

        // Plain double (not a pre-formatted string) so the copied DataTable's own
        // N2 formatting applies -- MetricsCellClass below colors Reward by sign,
        // same pattern SimulationPage uses for TraderCardItem/DecisionLogRow.
        private sealed record MetricsRow(int Episode, double Reward, double FinalEquity);

        // Most-recent-first, matching SimulationPage's LogRows convention.
        private List<MetricsRow> MetricsRows =>
            Service.RecentEpisodes
                .Select(m => new MetricsRow(m.EpisodeNumber, m.Reward, m.FinalEquity))
                .Reverse()
                .ToList();

        private static string MetricsCellClass(MetricsRow item, string propName) =>
            propName == nameof(MetricsRow.Reward) ? DataTablePnlClass(item.Reward) : "";

        // Flattened view of TradeStatistics for the copied DataTable (reflection-driven --
        // it renders one column per public property, in declaration order). Episode is a
        // string so the same shape covers both a single episode's number (Episode tab) and
        // the session row's "N episodes" label (Session tab). Win/loss amounts stay plain
        // doubles (not pre-formatted) so DataTable's own N2 formatting and
        // TraderStatsCellClass's sign-coloring both apply; time-in-market fields stay
        // pre-formatted strings since DataTable has no special-case TimeSpan formatting.
        private sealed record TraderStatsRow(
            string Episode, int Won, int Lost, double BiggestWin, double BiggestLoss,
            double AverageWin, double AverageLoss, int Buys, int Sells,
            string TimeInMarket, string TimeInLong, string TimeInShort,
            double MaxEquity, double MaxDrawdown, int LongestWinStreak, int LongestLossStreak);

        private static string TimeSpanStr(TimeSpan ts) => ts.ToString(@"d\.hh\:mm\:ss");

        private static TraderStatsRow ToTraderStatsRow(string episodeLabel, TradeStatistics s) => new(
            episodeLabel, s.WinCount, s.LossCount, s.BiggestWin, s.BiggestLoss,
            s.AverageWin, s.AverageLoss, s.BuyCount, s.SellCount,
            TimeSpanStr(s.TimeInMarket), TimeSpanStr(s.TimeInLong), TimeSpanStr(s.TimeInShort),
            s.MaxEquity, s.MaxDrawdown, s.LongestWinStreak, s.LongestLossStreak);

        // Returns the copied DataTable's own scoped color classes (its CSS is isolated to
        // that component). MaxDrawdown/Lost/LongestLossStreak are magnitudes that are never
        // negative numbers themselves, so they're pinned red whenever they're above zero
        // rather than relying on DataTablePnlClass's sign check.
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
            _ => "",
        };

        private static string DataTablePnlClass(double v) => v switch
        {
            > 0 => "data-table-value-positive",
            < 0 => "data-table-value-negative",
            _ => "",
        };

        // One row per completed episode (most-recent-first, same convention as
        // MetricsRows) -- each episode's own Trader instance, so equity/drawdown
        // reset every row rather than carrying over from the previous episode.
        private List<TraderStatsRow> EpisodeTraderStatsRows =>
            Service.RecentEpisodeTrades
                .Select(e => ToTraderStatsRow(
                    e.EpisodeNumber.ToString(CultureInfo.InvariantCulture),
                    TradeStatistics.From(e.Trades, e.MaxEquity, e.MaxDrawdown)))
                .Reverse()
                .ToList();

        // Single-row rollup pooling every retained episode's trades into one
        // TradeStatistics call -- MaxEquity/MaxDrawdown take the worst single
        // episode's extreme (each episode's Trader starts fresh, so there's no
        // continuous equity curve to walk across episode boundaries). Bounded to
        // the same recent-episode window as everything else here (see
        // RlTrainingService.RecentEpisodeTrades), so on a long run this is "the
        // session so far, capped" rather than strictly every episode ever run.
        private List<TraderStatsRow> SessionTraderStatsRows
        {
            get
            {
                var episodes = Service.RecentEpisodeTrades;
                if (episodes.Count == 0) return new List<TraderStatsRow>();

                var trades = episodes.SelectMany(e => e.Trades).ToList();
                var stats = TradeStatistics.From(trades, episodes.Max(e => e.MaxEquity), episodes.Max(e => e.MaxDrawdown));
                return new List<TraderStatsRow> { ToTraderStatsRow($"{episodes.Count} episodes", stats) };
            }
        }
    }
}
