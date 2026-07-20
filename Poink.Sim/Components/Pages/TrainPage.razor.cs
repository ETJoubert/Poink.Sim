using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
    public partial class TrainPage : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;
        [Inject] private ITickRepository TickRepository { get; set; } = default!;

        private string _brainName = "";
        private string _strategyName = "";
        private int _episodeCount = 100;
        private int _ticksPerEpisode = 500;
        private double _startingCash = 10_000;
        private double _learningRate = 0.001;
        private string? _startError;

        // ── Data source ──
        // Mirrors SimulationPage's table-select pattern: dummy synthetic ticks by
        // default (no Postgres needed), or a real historic table+range that gets
        // loaded once and randomly windowed per episode (see RlTrainingService).
        private bool _useHistoricData;
        private List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> _availableTables = new();
        private bool _loadingTables;
        private bool _tableListFailed;
        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        private string SelectedTable
        {
            get => _table;
            set
            {
                _table = value;
                var meta = _availableTables.FirstOrDefault(t => t.Table == value);
                if (meta.MaxTime is not null)
                {
                    _to = meta.MaxTime.Value;
                    _from = meta.MinTime is not null && meta.MaxTime.Value - meta.MinTime.Value < TimeSpan.FromDays(1)
                        ? meta.MinTime.Value
                        : meta.MaxTime.Value.AddDays(-1);
                }
            }
        }

        private async Task InitTables()
        {
            _loadingTables = true;
            _tableListFailed = false;
            StateHasChanged();

            try
            {
                _availableTables = await Task.Run(() => TickRepository.RefreshTickTableStats());
                if (_availableTables.Count > 0)
                    SelectedTable = _availableTables[0].Table;
            }
            catch
            {
                _tableListFailed = true;
            }
            finally
            {
                _loadingTables = false;
            }
        }

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
            if (_useHistoricData && string.IsNullOrWhiteSpace(_table)) { _startError = "pick a symbol"; return; }
            if (_useHistoricData && _to <= _from) { _startError = "To must be after From"; return; }

            var dataSource = _useHistoricData
                ? new RlDataSourceConfig(true, _table, _from, _to)
                : RlDataSourceConfig.Dummy;

            if (!Service.TryStartTraining(
                    _brainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate, dataSource))
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
