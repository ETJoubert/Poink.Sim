using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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

        // Which Start button was last used -- drives CurrentDatasetDescription
        // below (SaveModel's dataset tag), since there's no live checkbox to
        // read anymore.
        private bool _datasetIsHistoric;

        // Read by TrainPage's top-bar Start Training button after it awaits
        // StartTraining(), so it knows whether to jump back to this tab to show
        // the error (success already jumps to Live via OnTrainingStarted).
        public string? StartError => _startError;

        // ── Data source ──
        // Mirrors SimulationPage's table-select pattern -- a symbol+range is
        // always picked here, but which one of the two Start Training buttons
        // (TrainPage's top bar) the user clicks decides whether it's actually
        // used: the historic button streams a randomly-windowed slice per
        // episode straight from Postgres (see RlTrainingService.
        // RandomEpisodeWindow), the synthetic button ignores this entirely.
        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose()
        {
            Service.OnChanged -= HandleServiceChanged;
        }

        private bool ValidateCommonStartParams()
        {
            if (string.IsNullOrWhiteSpace(BrainName)) { _startError = "pick a brain"; return false; }
            if (string.IsNullOrWhiteSpace(_strategyName)) { _startError = "pick a strategy"; return false; }
            if (_episodeCount < 1) { _startError = "episode count must be positive"; return false; }
            if (_ticksPerEpisode < 2) { _startError = "ticks per episode must be at least 2"; return false; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return false; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return false; }
            return true;
        }

        // Public: TrainPage's top-bar Start Training button calls into this via
        // @ref, since the button now lives outside this tab (see StartError).
        // Historic: each episode streams its own randomly-windowed slice of the
        // selected symbol/range straight from Postgres.
        public async Task StartTraining()
        {
            _startError = null;
            if (!ValidateCommonStartParams()) return;
            if (string.IsNullOrWhiteSpace(_table)) { _startError = "pick a symbol"; return; }
            if (_to <= _from) { _startError = "To must be after From"; return; }

            _datasetIsHistoric = true;
            if (!Service.TryStartTraining(
                    BrainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate, (_table, _from, _to)))
                _startError = Service.Error ?? "a training run is already in progress";

            // Jump straight to the live view once a run is actually kicked off --
            // that's the whole point of the tab existing.
            if (_startError is null) await OnTrainingStarted.InvokeAsync();
        }

        // Public: TrainPage's top-bar Start Training (Synthetic) button. Ignores
        // whatever symbol/range is picked above -- same synthetic random-walk
        // path as Simulation's Load Dummy Data, so training can be exercised
        // without a running database.
        public async Task StartTrainingSynthetic()
        {
            _startError = null;
            if (!ValidateCommonStartParams()) return;

            _datasetIsHistoric = false;
            if (!Service.TryStartTraining(
                    BrainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate, historicRange: null))
                _startError = Service.Error ?? "a training run is already in progress";

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
        // "synthetic" is all there is to say about it. Reflects whichever Start
        // button was last used, not the live form state (SaveModel is fine to
        // click after tweaking the form since -- see its own doc comment).
        private string CurrentDatasetDescription =>
            _datasetIsHistoric && !string.IsNullOrWhiteSpace(_table)
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

        // Relays BrainStrategyPicker's own callback up to whatever bound BrainName
        // on this component (TrainPage, via @bind-BrainName) -- a plain @bind-BrainName
        // here would only rebind this component's local parameter, not propagate further.
        private Task HandleBrainNameChanged(string value) => BrainNameChanged.InvokeAsync(value);
    }
}

