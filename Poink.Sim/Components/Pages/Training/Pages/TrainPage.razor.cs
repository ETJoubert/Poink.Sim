using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

        // ── Data Range Controls ──
        // Mirrors SimulationPage's table-select pattern -- a symbol+range is
        // always picked here, but which Start Training button gets clicked
        // decides whether it's actually used: the historic button streams a
        // randomly-windowed slice per episode straight from Postgres (see
        // RlTrainingService.RandomEpisodeWindow), the synthetic button ignores
        // this entirely.
        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        // ── Brain Type Control ──
        private string _brainName = "";

        // ── Data Structure Controls ──
        // How raw ticks get shaped into bars, and how far back the volume-profile
        // brain looks over that same bar data -- independent of how many bars
        // one episode/run consists of (see Training Controls below).
        private int _barWidthMinutes = 1;
        private int _volumeProfileLookbackBars = 50;

        // ── Training Controls ──
        // The algorithm, plus how much data each episode/run consists of.
        private string _strategyName = "";
        private int _episodeCount = 100;
        private int _episodeBars = 200;
        private double _learningRate = 0.001;

        // ── Trader Controls ──
        private double _startingCash = 10_000;

        private string? _startError;

        // Which Start button was last used -- drives CurrentDatasetDescription
        // below (SaveModel's dataset tag), since there's no live checkbox to
        // read anymore.
        private bool _datasetIsHistoric;

        private string _saveName = "";

        // Drives the header's Save Model reveal -- clicking SAVE MODEL swaps it
        // for the name input/Save/Cancel group rather than always showing the
        // input, so the header stays compact until the user actually wants to save.
        private bool _showSaveInput;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (Service.BrainOptions.Count > 0) _brainName = Service.BrainOptions[0].Name;
            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        // Inlined here (rather than through BrainStrategyPicker, now removed) so
        // the Strategy select can sit in the Training Controls section while
        // BrainSelect sits in its own Brain Type section -- see TrainPage.razor.
        private void OnStrategyChanged(ChangeEventArgs e) => _strategyName = e.Value?.ToString() ?? "";

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
            if (_episodeBars < 1) { _startError = "bars per episode must be at least 1"; return false; }
            if (_barWidthMinutes < 1) { _startError = "bar width must be at least 1 minute"; return false; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return false; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return false; }
            if (_volumeProfileLookbackBars < 1) { _startError = "volume profile lookback must be at least 1 bar"; return false; }
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
                    _brainName, _strategyName, _episodeCount, _episodeBars, _barWidthMinutes, _startingCash, _learningRate,
                    _volumeProfileLookbackBars, (_table, _from, _to)))
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
                    _brainName, _strategyName, _episodeCount, _episodeBars, _barWidthMinutes, _startingCash, _learningRate,
                    _volumeProfileLookbackBars, historicRange: null))
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
            if (string.IsNullOrWhiteSpace(_saveName)) return;

            if (Service.SaveBrain(_saveName, _brainName, _strategyName, CurrentDatasetDescription,
                    _episodeCount, _episodeBars, _barWidthMinutes, _startingCash, _learningRate, _volumeProfileLookbackBars))
            {
                _saveName = "";
                _showSaveInput = false;
            }
        }

        private void ShowSaveModelInput() => _showSaveInput = true;

        private void CancelSaveModel()
        {
            _showSaveInput = false;
            _saveName = "";
        }

        private void OnSaveNameKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter") SaveModel();
            else if (e.Key == "Escape") CancelSaveModel();
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
    }
}
