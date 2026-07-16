using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
    public partial class TrainPage : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        private string _brainName = "";
        private string _strategyName = "";
        private int _episodeCount = 100;
        private int _ticksPerEpisode = 500;
        private double _startingCash = 10_000;
        private double _learningRate = 0.001;
        private string? _startError;

        private enum TrainTab { Setup, Live }
        private TrainTab _activeTab = TrainTab.Setup;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (Service.BrainNames.Count > 0) _brainName = Service.BrainNames[0];
            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private void StartTraining()
        {
            _startError = null;

            if (string.IsNullOrWhiteSpace(_brainName)) { _startError = "pick a brain"; return; }
            if (string.IsNullOrWhiteSpace(_strategyName)) { _startError = "pick a strategy"; return; }
            if (_episodeCount < 1) { _startError = "episode count must be positive"; return; }
            if (_ticksPerEpisode < 2) { _startError = "ticks per episode must be at least 2"; return; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return; }

            if (!Service.TryStartTraining(_brainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate))
                _startError = Service.Error ?? "a training run is already in progress";

            // Jump straight to the live view once a run is actually kicked off --
            // that's the whole point of the tab existing.
            if (_startError is null) _activeTab = TrainTab.Live;
        }

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

        private sealed record MetricsRow(int Episode, string Reward, string FinalEquity);

        // Most-recent-first, matching SimulationPage's LogRows convention.
        private List<object> MetricsRows =>
            Service.RecentEpisodes
                .Select(object (m) => new MetricsRow(m.EpisodeNumber, m.Reward.ToString("N2"), m.FinalEquity.ToString("N2")))
                .Reverse()
                .ToList();
    }
}
