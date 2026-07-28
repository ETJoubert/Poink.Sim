using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Compact, single-viewport alternative to TrainLiveTab -- everything on
    // one screen (KPI tiles, gauges, reward/trend charts, episode heatmap,
    // action mix, equity sparkline, training log) instead of TrainLiveTab's
    // long scrolling detail view with episode replay and stats tables.
    // Always fully live (no pause-and-freeze snapshot like TrainLiveTab --
    // a compact overview is exactly the view you'd want live at a glance).
    public partial class TrainDashboard : ComponentBase, IDisposable
    {
        private const double SparkWidth = 600;
        private const double SparkHeight = 80;
        private const double SparkPadY = 6;

        [Inject] private RlTrainingService Service { get; set; } = default!;

        [Parameter] public string BrainName { get; set; } = "";

        protected override void OnInitialized() => Service.OnChanged += HandleServiceChanged;

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private string StateLabel => Service.IsRunning
            ? (Service.IsPaused ? "PAUSED" : "TRAINING")
            : "IDLE";

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

        // Same reasoning as TrainLiveTab.RewardSpread -- derive from the
        // service's whole-run extremes, not RecentEpisodes' capped window.
        private double RewardSpread => Service.BestReward - Service.WorstReward;

        private (double StdDev, double Median) RewardDistribution
        {
            get
            {
                var episodes = Service.RecentEpisodes;
                return episodes.Count == 0 ? (0, 0) : (RewardStdDev(episodes), MedianReward(episodes));
            }
        }

        private double? LastEquity
        {
            get
            {
                var episodes = Service.RecentEpisodes;
                return episodes.Count == 0 ? null : episodes[^1].FinalEquity;
            }
        }

        // Aggregate delegates for TrendLineChart -- same statistics
        // TrainLiveTab plots, duplicated here rather than shared since
        // they're three small static functions and TrainDashboard otherwise
        // has no dependency on TrainLiveTab.
        private static double MedianReward(IReadOnlyList<RlEpisodeMetric> episodes)
        {
            var sorted = episodes.Select(e => e.Reward).OrderBy(r => r).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
        }

        private static double RewardStdDev(IReadOnlyList<RlEpisodeMetric> episodes)
        {
            var rewards = episodes.Select(e => e.Reward).ToList();
            double mean = rewards.Average();
            double variance = rewards.Sum(r => (r - mean) * (r - mean)) / rewards.Count;
            return Math.Sqrt(variance);
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

        // CSS width percentages must use '.' as the decimal separator
        // regardless of host locale (this host's is comma-decimal).
        private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // Colors a reward figure by its own sign -- never a fixed "best is
        // always green" mapping, since a negative "best" episode is still a
        // loss and must read as one.
        private static string SignClass(double v) => v switch
        {
            > 0 => "dash-value-positive",
            < 0 => "dash-value-negative",
            _ => ""
        };

        private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private sealed record EquitySpark(string PolylinePoints, string BaselineY, double FinalEquity, bool HasData);

        // Latest episode's equity curve, bar by bar -- min/max mapped to the
        // sparkline's height with a fixed baseline at the episode's starting
        // equity (LatestEpisodeBarEquity[0]), so a flat episode reads as a
        // flat line rather than an auto-zoomed noisy one.
        private EquitySpark EquitySparkline
        {
            get
            {
                var equity = Service.LatestEpisodeBarEquity;
                if (equity.Count == 0) return new EquitySpark("", "", 0, false);

                double min = equity.Min();
                double max = equity.Max();
                if (Math.Abs(max - min) < 1e-6) { min -= 1; max += 1; }

                double plotHeight = SparkHeight - 2 * SparkPadY;
                double MapY(double v) => SparkPadY + (1 - (v - min) / (max - min)) * plotHeight;
                double MapX(int i) => equity.Count <= 1 ? SparkWidth / 2 : i * (SparkWidth / (equity.Count - 1));

                string points = string.Join(" ", equity.Select((v, i) =>
                    $"{FormatCoord(MapX(i))},{FormatCoord(MapY(v))}"));

                return new EquitySpark(points, FormatCoord(MapY(equity[0])), equity[^1], true);
            }
        }

        private List<RlTrainingLogEntry> LogRows => Service.RecentLog.Reverse().ToList();

        private static string FormatLogTime(DateTime utc) => utc.ToLocalTime().ToString("HH:mm:ss");
    }
}
