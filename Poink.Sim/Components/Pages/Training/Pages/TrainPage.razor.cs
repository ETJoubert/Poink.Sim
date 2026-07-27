using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
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

        // ── Build Brain Controls ──
        // Assembles a new ComposedPolicyBrain from encoder/architecture parts
        // at runtime (see RlTrainingService.TryBuildBrain) -- an addition to,
        // not a replacement of, the BrainSelect dropdown above: a built brain
        // is just registered into the same list, then picked the normal way.
        private bool _showBuildBrain;
        private string _buildBrainName = "";

        // While on, the Name field tracks AutoBuildBrainName below instead of
        // _buildBrainName -- flips off the moment the user edits the field
        // directly (OnBuildBrainNameInput), and back on once a brain is
        // created, so the next build starts auto-named again.
        private bool _buildBrainNameAuto = true;
        private bool _includeBarWindow = true;
        private bool _includeVolumeProfile;
        private bool _includeTimeProfile;
        private bool _includeVolumeProfileRaw;
        private bool _includeTimeProfileRaw;

        // Per-encoder input width -- independent of each other and of
        // _lookbackBars below (see EncoderSizeOptions's doc comment for why
        // these are build-time-only, unlike the runtime-adjustable lookback).
        // Defaults match EncoderSizeOptions' own defaults, today's original
        // hardcoded bucket/window counts.
        private int _barWindowSize = 20;
        private int _volumeProfileBucketCount = 20;
        private int _timeProfileBucketCount = 20;
        private int _volumeProfileRawBucketCount = 20;
        private int _timeProfileRawBucketCount = 20;

        private ArchitectureKind _architectureKind = ArchitectureKind.Mlp;

        // One entry per MLP hidden layer, in network order -- edited via the
        // Structure card's per-layer number inputs (+ ADD LAYER/remove ×)
        // instead of a single comma-delimited text field. Can be emptied down
        // to zero layers (a linear model), same as clearing the old text field
        // could.
        private List<int> _mlpHiddenSizes = [32, 16];
        private int _lstmHiddenSize = 64;
        private int _lstmNumLayers = 2;

        // Which extra (non-direction) decisions the built brain's network
        // also makes -- see ActionOutputKind. All optional (unlike the
        // encoder checkboxes, at least one of which is required).
        private bool _includeVolumeOutput;
        private bool _includeStopLossOutput;
        private bool _includeTakeProfitOutput;

        // Which of Hold/EnterLong/EnterShort/Close the built brain's
        // direction pick is narrowed to -- default all 4 (today's original
        // behavior), but every one is individually optional, same as the
        // action-output checkboxes above (see SimulationAction's doc
        // comment); CanCreateBrain requires at least one stay checked.
        private bool _includeHold = true;
        private bool _includeEnterLong = true;
        private bool _includeEnterShort = true;
        private bool _includeClose = true;
        private string? _buildBrainStatus;

        // ── Data Structure Controls ──
        // How raw ticks get shaped into bars, and how far back a Volume/Time
        // Profile brain looks over that same bar data -- independent of how
        // many bars one episode/run consists of (see Training Controls below).
        private int _barWidthMinutes = 1;
        private int _lookbackBars = 50;

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

        // Gates the Data Structure card -- Profile lookback only means something
        // for a brain composed with a Volume/Time Profile (raw or not) encoder
        // (see IHasLookbackBars); a plain Bar Window brain (e.g. MLP/LSTM Policy)
        // ignores it entirely, so hide the field instead of showing a dead control.
        private bool BrainHasProfileLookback => Service.GetLookbackBars(_brainName) is not null;

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        // Inlined here (rather than through BrainStrategyPicker, now removed) so
        // the Strategy select can sit in the Training Controls section while
        // BrainSelect sits in its own Brain Type section -- see TrainPage.razor.
        private void OnStrategyChanged(ChangeEventArgs e) => _strategyName = e.Value?.ToString() ?? "";

        private void OnRewardPolicyChanged(ChangeEventArgs e) => _rewardPolicyName = e.Value?.ToString() ?? "";

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private void ToggleBuildBrain()
        {
            _showBuildBrain = !_showBuildBrain;
            _buildBrainStatus = null;
        }

        private void ToggleControls() => _controlsCollapsed = !_controlsCollapsed;

        private void OnArchitectureKindChanged(ChangeEventArgs e) =>
            _architectureKind = Enum.Parse<ArchitectureKind>(e.Value?.ToString() ?? nameof(ArchitectureKind.Mlp));

        // Mirrors the old ParseHiddenSizes(text)'s n > 0 filter -- a layer
        // cleared to 0 (or left blank) drops out rather than building a
        // zero-width layer.
        private IReadOnlyList<int> EffectiveMlpHiddenSizes => _mlpHiddenSizes.Where(n => n > 0).ToList();

        private void AddHiddenLayer() => _mlpHiddenSizes.Add(16);

        private void RemoveHiddenLayer(int index) => _mlpHiddenSizes.RemoveAt(index);

        private void OnHiddenLayerSizeChanged(int index, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int size)) _mlpHiddenSizes[index] = size;
        }

        // LSTM needs a sequence-bearing part (Bar Window) to narrow() a
        // sequence out of -- see LstmArchitecture's doc comment; mirrored here
        // so the Create button disables instead of only failing after a click.
        private bool CanCreateBrain =>
            !string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
            && (_includeBarWindow || _includeVolumeProfile || _includeTimeProfile || _includeVolumeProfileRaw || _includeTimeProfileRaw)
            && (_includeHold || _includeEnterLong || _includeEnterShort || _includeClose)
            && (_architectureKind != ArchitectureKind.Lstm || _includeBarWindow)
            && (!_includeBarWindow || _barWindowSize >= 1)
            && (!_includeVolumeProfile || _volumeProfileBucketCount >= 1)
            && (!_includeTimeProfile || _timeProfileBucketCount >= 1)
            && (!_includeVolumeProfileRaw || _volumeProfileRawBucketCount >= 1)
            && (!_includeTimeProfileRaw || _timeProfileRawBucketCount >= 1);

        // Canonical part order (Bar Window before Volume Profile before Time
        // Profile) regardless of checkbox click order -- CompositeEncoder
        // requires any sequence-bearing part to be listed first (see its doc
        // comment). Shared by CreateBrain and TotalInputCount so the two never
        // drift apart.
        private IReadOnlyList<EncoderKind> SelectedEncoderKinds
        {
            get
            {
                var kinds = new List<EncoderKind>();
                if (_includeBarWindow) kinds.Add(EncoderKind.BarWindow);
                if (_includeVolumeProfile) kinds.Add(EncoderKind.VolumeProfile);
                if (_includeTimeProfile) kinds.Add(EncoderKind.TimeProfile);
                if (_includeVolumeProfileRaw) kinds.Add(EncoderKind.VolumeProfileRaw);
                if (_includeTimeProfileRaw) kinds.Add(EncoderKind.TimeProfileRaw);
                return kinds;
            }
        }

        private EncoderSizeOptions CurrentEncoderSizeOptions => new(
            _barWindowSize, _volumeProfileBucketCount, _timeProfileBucketCount, _volumeProfileRawBucketCount, _timeProfileRawBucketCount);

        // Live input-count readout for the Input Structure card -- reuses
        // BrainCatalog.CreateEncoder (the same factory CreateBrain itself
        // calls into via Service.TryBuildBrain) as the single source of truth
        // for each encoder's FeatureCount, instead of duplicating that
        // arithmetic here. Trader State is always included (see BrainCatalog's
        // doc comment on why it's not a pickable EncoderKind).
        private int TotalInputCount =>
            new TraderStateEncoder().FeatureCount
            + SelectedEncoderKinds.Sum(k => BrainCatalog.CreateEncoder(k, CurrentEncoderSizeOptions).FeatureCount);

        // {Architecture}_{Layers}_{Data}, e.g. MLP_L32-L16_dBW-dVP-dTP-dAS -- mirrors
        // CreateBrain's own encoder/architecture choices below, so the generated
        // name always describes exactly what CREATE BRAIN is about to build.
        // Trader State has no checkbox (always included) but still gets a
        // "dAS" tag here for the same reason it gets a disabled-checked
        // checkbox above: visible parity with the other data sources.
        private string AutoBuildBrainName
        {
            get
            {
                string arch = _architectureKind switch
                {
                    ArchitectureKind.Mlp => "MLP",
                    ArchitectureKind.Lstm => "LSTM",
                    _ => _architectureKind.ToString().ToUpperInvariant(),
                };

                string layers = _architectureKind == ArchitectureKind.Mlp
                    ? string.Join("-", EffectiveMlpHiddenSizes.Select(n => $"L{n}"))
                    : $"H{_lstmHiddenSize}-N{_lstmNumLayers}";

                var dataParts = new List<string>();
                if (_includeBarWindow) dataParts.Add("dBW");
                if (_includeVolumeProfile) dataParts.Add("dVP");
                if (_includeTimeProfile) dataParts.Add("dTP");
                if (_includeVolumeProfileRaw) dataParts.Add("dVPR");
                if (_includeTimeProfileRaw) dataParts.Add("dTPR");
                dataParts.Add("dAS");
                string data = string.Join("-", dataParts);

                string name = string.IsNullOrEmpty(layers) ? $"{arch}_{data}" : $"{arch}_{layers}_{data}";

                // Excluded (not included) directions are called out here --
                // shorter than listing all the ones still enabled, and the
                // common case (all 4) needs no tag at all.
                var actionParts = new List<string>();
                if (!_includeHold) actionParts.Add("noHOLD");
                if (!_includeEnterLong) actionParts.Add("noLONG");
                if (!_includeEnterShort) actionParts.Add("noSHORT");
                if (!_includeClose) actionParts.Add("noCLOSE");
                if (_includeVolumeOutput) actionParts.Add("aVOL");
                if (_includeStopLossOutput) actionParts.Add("aSL");
                if (_includeTakeProfitOutput) actionParts.Add("aTP");
                return actionParts.Count == 0 ? name : $"{name}_{string.Join("-", actionParts)}";
            }
        }

        private string EffectiveBuildBrainName => _buildBrainNameAuto ? AutoBuildBrainName : _buildBrainName;

        private void OnBuildBrainNameInput(ChangeEventArgs e)
        {
            _buildBrainNameAuto = false;
            _buildBrainName = e.Value?.ToString() ?? "";
        }

        private void OnToggleAutoBuildBrainName(ChangeEventArgs e)
        {
            _buildBrainNameAuto = e.Value is bool isAuto && isAuto;
            if (!_buildBrainNameAuto) _buildBrainName = AutoBuildBrainName;
        }

        private void CreateBrain()
        {
            var name = EffectiveBuildBrainName;
            var encoderKinds = SelectedEncoderKinds;

            IReadOnlyList<int> hiddenSizes = _architectureKind == ArchitectureKind.Mlp
                ? EffectiveMlpHiddenSizes
                : [_lstmHiddenSize, _lstmNumLayers];

            var actionOutputKinds = new List<ActionOutputKind>();
            if (_includeVolumeOutput) actionOutputKinds.Add(ActionOutputKind.Volume);
            if (_includeStopLossOutput) actionOutputKinds.Add(ActionOutputKind.StopLoss);
            if (_includeTakeProfitOutput) actionOutputKinds.Add(ActionOutputKind.TakeProfit);

            // Canonical order (matches ComposedPolicyBrain.AllDirections)
            // regardless of checkbox click order.
            var enabledDirections = new List<SimulationAction>();
            if (_includeHold) enabledDirections.Add(SimulationAction.Hold);
            if (_includeEnterLong) enabledDirections.Add(SimulationAction.EnterLong);
            if (_includeEnterShort) enabledDirections.Add(SimulationAction.EnterShort);
            if (_includeClose) enabledDirections.Add(SimulationAction.Close);

            if (Service.TryBuildBrain(name, encoderKinds, _architectureKind, hiddenSizes, actionOutputKinds, enabledDirections, CurrentEncoderSizeOptions))
            {
                _buildBrainStatus = $"built '{name}' -- pick it above to train it";
                _brainName = name;
                _buildBrainName = "";
                _buildBrainNameAuto = true;
                _showBuildBrain = false;
            }
            else
            {
                _buildBrainStatus = Service.Error;
            }
        }

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
            if (_barWidthMinutes < 1) { _startError = "bar width must be at least 1 minute"; return false; }
            if (_startingCash <= 0) { _startError = "starting cash must be positive"; return false; }
            if (_learningRate <= 0) { _startError = "learning rate must be positive"; return false; }
            if (_lookbackBars < 1) { _startError = "profile lookback must be at least 1 bar"; return false; }
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
                    _lookbackBars, (_table, _from, _to), CurrentTraderSettings, pauseAfterFirstEpisode))
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
                    _episodeBars, _barWidthMinutes, _startingCash, _learningRate, _lookbackBars,
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
