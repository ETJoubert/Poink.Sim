using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Split out of TrainPage so the Live tab owns its own re-render cycle,
    // independent of the Setup tab -- see TrainSetupTab's doc comment. BrainName
    // is the one piece of Setup state this side still needs, to visualize
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

        private sealed record InputBar(string Label, double Value, string HeightPct, string ColorCss);

        // Bar height is scaled relative to the current window's own max magnitude
        // (same "diverging, batch-relative intensity" treatment as EpisodeGrid) --
        // these are small percent-change values, so an absolute scale would make
        // every bar look identical.
        private List<InputBar> PriceWindowBars
        {
            get
            {
                var features = PriceWindowFeatures;
                if (features.Count == 0) return new List<InputBar>();

                double maxAbs = features.Max(f => Math.Abs(f.Value));
                return features.Select(f =>
                {
                    double heightPct = maxAbs > 0 ? Math.Clamp(Math.Abs(f.Value) / maxAbs, 0.08, 1.0) * 100 : 8;
                    string colorVar = f.Value switch { > 0 => "--color-success", < 0 => "--color-error", _ => "--color-muted" };
                    return new InputBar(f.Label, f.Value, Pct(heightPct), $"var({colorVar})");
                }).ToList();
            }
        }

        private sealed record MetricsRow(int Episode, string Reward, string FinalEquity);

        // Most-recent-first, matching SimulationPage's LogRows convention.
        private List<object> MetricsRows =>
            Service.RecentEpisodes
                .Select(object (m) => new MetricsRow(m.EpisodeNumber, m.Reward.ToString("N2"), m.FinalEquity.ToString("N2")))
                .Reverse()
                .ToList();
    }
}
