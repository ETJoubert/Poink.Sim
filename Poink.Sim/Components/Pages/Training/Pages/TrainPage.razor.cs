using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Repository;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
    // Flat, single-view page (no separate Setup/Live tabs) -- mirrors
    // SimulationPage: every control needed to kick off a run lives in the
    // sticky topbar, and the content below (episodes/reward/network activity)
    // is always visible rather than gated behind a tab switch.
    public partial class TrainPage : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        // ── Data source ──
        // Mirrors SimulationPage's table-select pattern -- a symbol+range is
        // always picked here, but which Start Training button gets clicked
        // decides whether it's actually used: the historic button streams a
        // randomly-windowed slice per episode straight from Postgres (see
        // RlTrainingService.RandomEpisodeWindow), the synthetic button ignores
        // this entirely.
        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        // ── Brain / run parameters ──
        private string _brainName = "";
        private string _strategyName = "";
        private int _episodeCount = 100;
        private int _ticksPerEpisode = 500;
        private double _startingCash = 10_000;
        private double _learningRate = 0.001;
        private string? _startError;

        // Which Start button was last used -- drives CurrentDatasetDescription
        // below (SaveModel's dataset tag), since there's no live checkbox to
        // read anymore.
        private bool _datasetIsHistoric;

        private string _saveName = "";

        // Collapses the (potentially long) Saved brains table -- the current
        // in-use snapshot is already visible in the sticky header above, so
        // this table only needs expanding when actually managing snapshots.
        private bool _savedBrainsExpanded = true;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (Service.BrainOptions.Count > 0) _brainName = Service.BrainOptions[0].Name;
            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        // Drives the sticky header's progress bar.
        private int ProgressPercent =>
            Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);

        // Which saved snapshot (if any) the currently-picked brain's live weights
        // match -- shown in the sticky header so it stays visible regardless of
        // scroll position, mirroring how SimulationPage's header always shows
        // the brain/trader currently being viewed. Null for a heuristic brain,
        // or a neural brain whose weights were never saved / were trained
        // further since.
        private SavedBrainSummary? CurrentActiveSnapshot =>
            string.IsNullOrWhiteSpace(_brainName) ? null : Service.GetActiveSnapshot(_brainName);

        private bool ValidateCommonStartParams()
        {
            if (string.IsNullOrWhiteSpace(_brainName)) { _startError = "pick a brain"; return false; }
            if (string.IsNullOrWhiteSpace(_strategyName)) { _startError = "pick a strategy"; return false; }
            if (_episodeCount < 1) { _startError = "episode count must be positive"; return false; }
            if (_ticksPerEpisode < 2) { _startError = "ticks per episode must be at least 2"; return false; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return false; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return false; }
            return true;
        }

        // Historic: each episode streams its own randomly-windowed slice of the
        // selected symbol/range straight from Postgres.
        private void StartTraining()
        {
            _startError = null;
            if (!ValidateCommonStartParams()) return;
            if (string.IsNullOrWhiteSpace(_table)) { _startError = "pick a symbol"; return; }
            if (_to <= _from) { _startError = "To must be after From"; return; }

            _datasetIsHistoric = true;
            if (!Service.TryStartTraining(
                    _brainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate, (_table, _from, _to)))
                _startError = Service.Error ?? "a training run is already in progress";
        }

        // Skips Postgres entirely (synthetic random-walk ticks) -- same as
        // Simulation's Load Dummy Data, so training can be exercised without a
        // running database. Ignores whatever symbol/range is picked above.
        private void StartTrainingSynthetic()
        {
            _startError = null;
            if (!ValidateCommonStartParams()) return;

            _datasetIsHistoric = false;
            if (!Service.TryStartTraining(
                    _brainName, _strategyName, _episodeCount, _ticksPerEpisode, _startingCash, _learningRate, historicRange: null))
                _startError = Service.Error ?? "a training run is already in progress";
        }

        // Fine to click mid-run -- SaveBrain reads the brain's own live
        // Network, which a training strategy mutates in place, so this just
        // snapshots whatever it currently holds, tagged with this page's
        // current strategy/parameter/data-source fields (not necessarily what
        // the last completed run actually used, if the user tweaked the form
        // since).
        private void SaveModel()
        {
            if (Service.SaveBrain(_saveName, _brainName, _strategyName, CurrentDatasetDescription,
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
    }
}
