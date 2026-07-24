using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
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
        private ArchitectureKind _architectureKind = ArchitectureKind.Mlp;
        private string _mlpHiddenSizesText = "32,16";
        private int _lstmHiddenSize = 64;
        private int _lstmNumLayers = 2;
        private string? _buildBrainStatus;

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

            if (Service.BrainOptions.Count > 0) _brainName = Service.BrainOptions[0].Name;
            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        // Inlined here (rather than through BrainStrategyPicker, now removed) so
        // the Strategy select can sit in the Training Controls section while
        // BrainSelect sits in its own Brain Type section -- see TrainPage.razor.
        private void OnStrategyChanged(ChangeEventArgs e) => _strategyName = e.Value?.ToString() ?? "";

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private void ToggleBuildBrain()
        {
            _showBuildBrain = !_showBuildBrain;
            _buildBrainStatus = null;
        }

        private void ToggleControls() => _controlsCollapsed = !_controlsCollapsed;

        private void OnArchitectureKindChanged(ChangeEventArgs e) =>
            _architectureKind = Enum.Parse<ArchitectureKind>(e.Value?.ToString() ?? nameof(ArchitectureKind.Mlp));

        // LSTM needs a sequence-bearing part (Bar Window) to narrow() a
        // sequence out of -- see LstmArchitecture's doc comment; mirrored here
        // so the Create button disables instead of only failing after a click.
        private bool CanCreateBrain =>
            !string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
            && (_includeBarWindow || _includeVolumeProfile)
            && (_architectureKind != ArchitectureKind.Lstm || _includeBarWindow);

        // {Architecture}_{Layers}_{Data}, e.g. MLP_L32-L16_dBW-dVP-dAS -- mirrors
        // CreateBrain's own encoder/architecture choices below, so the generated
        // name always describes exactly what CREATE BRAIN is about to build.
        // Account State has no checkbox (always included) but still gets a
        // "dAS" tag here for the same reason it gets a disabled-checked
        // checkbox above: visible parity with the other two data sources.
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
                    ? string.Join("-", ParseHiddenSizes(_mlpHiddenSizesText).Select(n => $"L{n}"))
                    : $"H{_lstmHiddenSize}-N{_lstmNumLayers}";

                var dataParts = new List<string>();
                if (_includeBarWindow) dataParts.Add("dBW");
                if (_includeVolumeProfile) dataParts.Add("dVP");
                dataParts.Add("dAS");
                string data = string.Join("-", dataParts);

                return string.IsNullOrEmpty(layers) ? $"{arch}_{data}" : $"{arch}_{layers}_{data}";
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

        // Canonical part order (Bar Window before Volume Profile) regardless of
        // checkbox click order -- CompositeEncoder requires any sequence-bearing
        // part to be listed first (see its doc comment).
        private void CreateBrain()
        {
            var name = EffectiveBuildBrainName;
            var encoderKinds = new List<EncoderKind>();
            if (_includeBarWindow) encoderKinds.Add(EncoderKind.BarWindow);
            if (_includeVolumeProfile) encoderKinds.Add(EncoderKind.VolumeProfile);

            IReadOnlyList<int> hiddenSizes = _architectureKind == ArchitectureKind.Mlp
                ? ParseHiddenSizes(_mlpHiddenSizesText)
                : [_lstmHiddenSize, _lstmNumLayers];

            if (Service.TryBuildBrain(name, encoderKinds, _architectureKind, hiddenSizes))
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

        private static IReadOnlyList<int> ParseHiddenSizes(string text) =>
            text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out int n) ? n : 0)
                .Where(n => n > 0)
                .ToList();

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

            if (!Service.TryStartTraining(
                    _brainName, _strategyName, _episodeCount, _episodeBars, _barWidthMinutes, _startingCash, _learningRate,
                    _volumeProfileLookbackBars, (_table, _from, _to)))
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
        // SavedBrainSummary's doc comment). Reflects the live Data Range form
        // state, not necessarily what the last completed run actually used
        // (SaveModel is fine to click after tweaking the form since -- see its
        // own doc comment).
        private string CurrentDatasetDescription => $"{_table} ({_from:yyyy-MM-dd HH:mm} to {_to:yyyy-MM-dd HH:mm})";
    }
}
