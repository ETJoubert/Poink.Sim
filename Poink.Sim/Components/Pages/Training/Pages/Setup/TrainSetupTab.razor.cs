using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Repository;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Split out of TrainPage so the Setup and Live tabs each own their own
    // state/re-render cycle -- only BrainName crosses the boundary (Live's
    // Network Activity panel visualizes whichever brain is currently picked
    // here), via the same @bind-X EventCallback pattern as BrainStrategyPicker.
    public partial class TrainSetupTab : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;
        [Inject] private ITickRepository TickRepository { get; set; } = default!;
        [Inject] private TickDataStore TickDataStore { get; set; } = default!;

        [Parameter] public string BrainName { get; set; } = "";
        [Parameter] public EventCallback<string> BrainNameChanged { get; set; }

        // Fired once TryStartTraining actually kicks a run off -- the parent
        // jumps to the Live tab, since that's the whole point of Start Training.
        [Parameter] public EventCallback OnTrainingStarted { get; set; }

        private string _strategyName = "";
        private int _episodeCount = 100;
        private int _ticksPerEpisode = 500;
        private double _startingCash = 10_000;
        private double _learningRate = 0.001;
        private string? _startError;
        private string _saveName = "";

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

        private (string Table, long Rows, DateTime? MinTime, DateTime? MaxTime) SelectedTableMeta =>
            _availableTables.FirstOrDefault(t => t.Table == _table);

        private DateTime SliderMin => SelectedTableMeta.MinTime ?? DateTime.UtcNow.AddDays(-30);
        private DateTime SliderMax => SelectedTableMeta.MaxTime ?? DateTime.UtcNow;

        private bool IsDataReady => !string.IsNullOrWhiteSpace(_table) && TickDataStore.IsLoadedFor(_table, _from, _to);

        // Fetches and caches the actual tick rows for the selected table+range
        // (TickDataStore, shared with SimulationPage) -- separate from picking a
        // table, and from StartTraining() below, which now just consumes whatever's
        // already loaded here instead of re-reading Postgres itself.
        private void LoadData()
        {
            _startError = null;
            if (string.IsNullOrWhiteSpace(_table)) { _startError = "pick a symbol"; return; }
            if (_to <= _from) { _startError = "To must be after From"; return; }

            if (!TickDataStore.TryLoad(_table, _from, _to))
                _startError = "a load is already in progress";
        }

        // Fast: reads the tick_table_stats registry (small table, no scanning).
        private Task LoadTables() => FetchTables(() => TickRepository.ListTickTables());

        // Slow: rescans every ticks_* table (COUNT(*)/MIN/MAX) and rebuilds the
        // registry -- an explicit action so it never happens as a side effect of
        // just wanting the already-known table list.
        private Task RecountTables() => FetchTables(() => TickRepository.RefreshTickTableStats());

        private async Task FetchTables(Func<List<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)>> fetch)
        {
            _loadingTables = true;
            _tableListFailed = false;
            StateHasChanged();

            try
            {
                _availableTables = await Task.Run(fetch);
                if (_availableTables.Count > 0 && string.IsNullOrWhiteSpace(_table))
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

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;
            TickDataStore.OnChanged += HandleServiceChanged;

            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];

            // TickRepository is a singleton -- reuse another page's already-loaded
            // table list instead of making the user click a data button again here.
            if (TickRepository.HasCachedTables)
            {
                _availableTables = TickRepository.CachedTables.ToList();
                if (_availableTables.Count > 0 && string.IsNullOrWhiteSpace(_table))
                    SelectedTable = _availableTables[0].Table;
            }
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose()
        {
            Service.OnChanged -= HandleServiceChanged;
            TickDataStore.OnChanged -= HandleServiceChanged;
        }

        private async Task StartTraining()
        {
            _startError = null;

            if (string.IsNullOrWhiteSpace(BrainName)) { _startError = "pick a brain"; return; }
            if (string.IsNullOrWhiteSpace(_strategyName)) { _startError = "pick a strategy"; return; }
            if (_episodeCount < 1) { _startError = "episode count must be positive"; return; }
            if (_ticksPerEpisode < 2) { _startError = "ticks per episode must be at least 2"; return; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return; }

            List<Tick>? historicTicks = null;
            if (_useHistoricData)
            {
                if (string.IsNullOrWhiteSpace(_table)) { _startError = "pick a symbol"; return; }
                if (_to <= _from) { _startError = "To must be after From"; return; }
                historicTicks = TickDataStore.GetLoaded(_table, _from, _to);
                if (historicTicks is null) { _startError = "load the data first"; return; }
            }

            if (!Service.TryStartTraining(
                    BrainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate, historicTicks))
                _startError = Service.Error ?? "a training run is already in progress";

            // Jump straight to the live view once a run is actually kicked off --
            // that's the whole point of the tab existing.
            if (_startError is null) await OnTrainingStarted.InvokeAsync();
        }

        // Fine to click mid-run -- SaveBrain reads the brain's own live
        // Network, which a training strategy mutates in place, so this just
        // snapshots whatever it currently holds, tagged with this tab's
        // current strategy/parameter/data-source fields (not necessarily what
        // the last completed run actually used, if the user tweaked the form
        // since).
        private void SaveModel()
        {
            if (Service.SaveBrain(_saveName, BrainName, _strategyName, CurrentDatasetDescription,
                    _episodeCount, _ticksPerEpisode, _startingCash, _learningRate))
                _saveName = "";
        }

        // Just a reference string, not a link back to actual tick data (see
        // SavedBrainSummary's doc comment) -- Dummy data has no fixed range, so
        // "synthetic" is all there is to say about it.
        private string CurrentDatasetDescription =>
            _useHistoricData && !string.IsNullOrWhiteSpace(_table)
                ? $"{_table} ({_from:yyyy-MM-dd HH:mm} to {_to:yyyy-MM-dd HH:mm})"
                : "synthetic (random-walk)";

        private void LoadSavedBrain(SavedBrainRow row) => Service.LoadSavedBrain(row.Id);

        private void DeleteSavedBrain(SavedBrainRow row) => Service.DeleteSavedBrain(row.Id);

        // Flattens SavedBrainSummary into a DataTable-friendly shape -- the
        // generic DataTable<TItem> reflects over public properties directly
        // (see its View/Card rendering), so Parameters needs to already be a
        // display string rather than the nested BrainTrainingParameters object.
        private sealed record SavedBrainRow(
            long Id, string Name, string Brain, string Strategy, string Dataset,
            long WeightCount, string Parameters, double AvgReward, DateTime SavedAt);

        private List<SavedBrainRow> SavedBrainRows =>
            Service.SavedBrains.Select(b => new SavedBrainRow(
                b.Id, b.Name, b.BrainName, b.StrategyName, b.Dataset, b.WeightCount, b.Parameters.ToString(), b.AvgReward, b.SavedAt)).ToList();

        private int ProgressPercent =>
            Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);

        // Relays BrainStrategyPicker's own callback up to whatever bound BrainName
        // on this component (TrainPage, via @bind-BrainName) -- a plain @bind-BrainName
        // here would only rebind this component's local parameter, not propagate further.
        private Task HandleBrainNameChanged(string value) => BrainNameChanged.InvokeAsync(value);
    }
}
