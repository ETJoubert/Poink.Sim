using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;
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
        // Mirrors SimulationPage's table-select pattern -- the picked symbol+
        // range is what Start Training uses: each episode streams its own
        // randomly-windowed slice straight from Postgres (see
        // RlTrainingService.RandomEpisodeWindow).
        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        // ── Brain Type Control ──
        private string _brainName = "";

        // ── Build Brain Control ──
        // Assembling a new ComposedPolicyBrain from encoder/architecture parts
        // (see RlTrainingService.TryBuildBrain) lives entirely in BrainBuilder
        // now -- an addition to, not a replacement of, the BrainSelect
        // dropdown above: a built brain is just registered into the same
        // list, then picked the normal way. Visible is passed straight
        // through rather than wrapping the component in an @if, so a build
        // left mid-configuration survives being collapsed and reopened (see
        // BrainBuilder's own doc comment).
        private bool _showBuildBrain;

        // ── Data Structure Controls ──
        // How raw ticks get shaped into bars -- independent of how many bars
        // one episode/run consists of (see Training Controls below). A brain's
        // own per-set window/lookback (Bar Window size, Volume/Time Profile
        // lookback) is a build-time choice made in BrainBuilder now, not a
        // runtime setting here.
        private int _barWidthMinutes = 1;

        // ── Training Controls ──
        // The algorithm, plus how much data each episode/run consists of.
        private string _strategyName = "";
        private string _rewardPolicyName = "";
        private int _episodeCount = 100;
        private int _episodeBars = 200;
        private double _learningRate = 0.001;

        // ── Trader Controls ──
        private double _startingCash = 10_000;
        private double _leverage = 1;
        private double _commissionRatePercent = 0;
        private double _stopOutLevelPercent = 50;

        private string? _startError;

        private string _saveName = "";

        // Drives the header's Save Model reveal -- clicking SAVE MODEL swaps it
        // for the name input/Save/Cancel group rather than always showing the
        // input, so the header stays compact until the user actually wants to save.
        private bool _showSaveInput;

        // Collapses the Data Range/Brain Type/.../Reward Policy rows -- the
        // header (title, state, Save/Start actions) and progress bar stay
        // visible either way, since those matter regardless of whether the
        // run's parameters are being edited right now. Starts expanded so
        // nothing changes until a user opts into collapsing it.
        private bool _controlsCollapsed;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (TrainableBrainOptions.Count > 0) _brainName = TrainableBrainOptions[0].Name;
            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
            if (Service.RewardPolicyNames.Count > 0) _rewardPolicyName = Service.RewardPolicyNames[0];
        }

        // Heuristic brains (MomentumThresholdBrain, RandomBrain) have no trainable
        // weights -- IRlTrainingStrategy.CanTrain rejects anything but
        // IPolicyGradientBrain, so this page's picker only ever offers the ones
        // that can actually run here. BrainSelect still shows its NEURAL NETWORK/
        // HEURISTIC split generically (SimulationPage passes it the full,
        // unfiltered Service.BrainOptions for playback of either kind).
        private IReadOnlyList<BrainOption> TrainableBrainOptions =>
            Service.BrainOptions.Where(b => b.IsNeuralNetwork).ToList();

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        // Inlined here (rather than through BrainStrategyPicker, now removed) so
        // the Strategy select can sit in the Training Controls section while
        // BrainSelect sits in its own Brain Type section -- see TrainPage.razor.
        private void OnStrategyChanged(ChangeEventArgs e) => _strategyName = e.Value?.ToString() ?? "";

        private void OnRewardPolicyChanged(ChangeEventArgs e) => _rewardPolicyName = e.Value?.ToString() ?? "";

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private void ToggleBuildBrain() => _showBuildBrain = !_showBuildBrain;

        // BrainBuilder's OnBrainCreated callback -- points Brain Type at the
        // brain it just built and collapses the panel, mirroring what
        // BrainBuilder.CreateBrain used to do to TrainPage's own fields
        // directly before the two were split apart.
        private void HandleBrainCreated(string name)
        {
            _brainName = name;
            _showBuildBrain = false;
        }

        private void ToggleControls() => _controlsCollapsed = !_controlsCollapsed;

        // Swaps TrainLiveTab's full detail view for TrainDashboard's compact
        // one-viewport summary -- starts false so nothing changes until a
        // user opts into it (same reasoning as _controlsCollapsed above).
        private bool _dashboardView;

        private void ToggleDashboardView() => _dashboardView = !_dashboardView;

        // One block's time span for EpisodeCoverageTimeline -- mirrors
        // TryStartTraining's own fetchSpan sizing (barWidth * episodeBars,
        // minus its extra safety-margin bar) so a block lines up with what one
        // episode actually covers.
        private TimeSpan EpisodeBlockSpan => TimeSpan.FromMinutes(_barWidthMinutes) * _episodeBars;

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
            if (string.IsNullOrWhiteSpace(_rewardPolicyName)) { _startError = "pick a reward policy"; return false; }
            if (_episodeCount < 1) { _startError = "episode count must be positive"; return false; }
            if (_episodeBars < 1) { _startError = "bars per episode must be at least 1"; return false; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return false; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return false; }
            if (_leverage < 1) { _startError = "leverage must be at least 1"; return false; }
            if (_commissionRatePercent < 0) { _startError = "commission rate can't be negative"; return false; }
            if (_stopOutLevelPercent <= 0 || _stopOutLevelPercent > 100) { _startError = "stop-out level must be between 0 and 100%"; return false; }
            return true;
        }

        private TraderSettings CurrentTraderSettings => new(
            _leverage, _stopOutLevelPercent, new PercentOfNotionalCommission(_commissionRatePercent));

        // Historic: each episode streams its own randomly-windowed slice of the
        // selected symbol/range straight from Postgres. Collapses the config
        // panel once a run actually starts (not on STEP -- that's for careful
        // episode-by-episode work where the panel likely should stay open), so
        // the panels below get the room back without an extra click.
        private void StartTraining()
        {
            StartTrainingCore(pauseAfterFirstEpisode: false);
            if (_startError is null) _controlsCollapsed = true;
        }

        // STEP button: while a run is already paused, just advance it one
        // episode (Service already has everything it needs). Otherwise this
        // starts a fresh run exactly like Start, but pre-paused after its
        // first episode, so stepping can begin from episode 1.
        private void StepTraining()
        {
            if (Service.IsRunning) { Service.StepTraining(); return; }
            StartTrainingCore(pauseAfterFirstEpisode: true);
        }

        private void StartTrainingCore(bool pauseAfterFirstEpisode)
        {
            _startError = null;
            if (!ValidateCommonStartParams()) return;
            if (string.IsNullOrWhiteSpace(_table)) { _startError = "pick a symbol"; return; }
            if (_to <= _from) { _startError = "To must be after From"; return; }

            if (!Service.TryStartTraining(
                    _brainName, _strategyName, _rewardPolicyName, _episodeCount, _episodeBars, _barWidthMinutes, _startingCash, _learningRate,
                    (_table, _from, _to), CurrentTraderSettings, pauseAfterFirstEpisode))
                _startError = Service.Error ?? "a training run is already in progress";
        }

        private void PauseTraining() => Service.PauseTraining();

        private void ResumeTraining() => Service.ResumeTraining();

        private void StopTraining() => Service.StopTraining();

        // Fine to click mid-run -- SaveBrain reads the brain's own live
        // Network, which a training strategy mutates in place, so this just
        // snapshots whatever it currently holds, tagged with this page's
        // current strategy/parameter/data-source fields (not necessarily what
        // the last completed run actually used, if the user tweaked the form
        // since).
        private void SaveModel()
        {
            if (string.IsNullOrWhiteSpace(_saveName)) return;

            if (Service.SaveBrain(_saveName, _brainName, _strategyName, _rewardPolicyName, CurrentDatasetDescription,
                    _episodeBars, _barWidthMinutes, _startingCash, _learningRate,
                    CurrentTraderSettings))
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
        // SavedBrainSummary's doc comment). Reflects the live Data Range form
        // state, not necessarily what the last completed run actually used
        // (SaveModel is fine to click after tweaking the form since -- see its
        // own doc comment).
        private string CurrentDatasetDescription => $"{_table} ({_from:yyyy-MM-dd HH:mm} to {_to:yyyy-MM-dd HH:mm})";
    }
}
