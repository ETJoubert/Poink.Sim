using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Training;

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

        protected override void OnInitialized() => Service.OnChanged += HandleServiceChanged;

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        // Drives the Live tab's Network Activity animation -- the currently
        // selected brain's real architecture (see IPolicyGradientBrain.
        // DescribeNetworkShape), not a hardcoded shape.
        private IReadOnlyList<NetworkLayerShape> CurrentNetworkShape => Service.DescribeNetworkShape(BrainName);

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

        // MlpPolicyBrain.DescribeInputs labels the price-window entries "t-N" and
        // the account-state scalars by name (see that method) -- split on that
        // convention rather than a fixed count, so this keeps working if a future
        // brain's window size differs.
        private List<RlInputFeature> PriceWindowFeatures =>
            Service.LatestInputs?.Where(f => f.Label.StartsWith("t-", StringComparison.Ordinal)).ToList() ?? new List<RlInputFeature>();

        private List<RlInputFeature> ScalarFeatures =>
            Service.LatestInputs?.Where(f => !f.Label.StartsWith("t-", StringComparison.Ordinal)).ToList() ?? new List<RlInputFeature>();

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
