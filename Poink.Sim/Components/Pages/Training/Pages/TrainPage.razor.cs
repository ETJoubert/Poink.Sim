using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Training;
using Poink.Sim.Helpers;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;
using System.Linq;

namespace Poink.Sim.Components.Pages
{
    public partial class TrainPage : ComponentBase, IDisposable
    {
        [Inject] private IRlTrainingService Service { get; set; } = default!;

        [Inject] private IRiskPolicyRegistry RiskPolicyRegistry { get; set; } = default!;

        // Set via @ref on the Build panel -- StartTrainingCore asks it to
        // draft a live, unsaved brain from its current fields right before a
        // run starts (see BrainBuilder.PrepareBrainForTraining).
        private BrainBuilder? _brainBuilder;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        private string _brainName = "";

        private int _barWidthSeconds = 60;

        private string _strategyName = "";
        private string _rewardPolicyName = "";
        private int _episodeCount = 1000;
        private int _episodeBars = 20;
        private double _learningRate = 0.001;

        // Episode Timing card -- off by default, so an episode may start anywhere
        // in the Data Range above (including across midnight), exactly like
        // before this feature existed. Weekday/session are independent,
        // combinable filters (see EpisodeTimeConstraint's doc comment): "Days" is
        // just every weekday with the full 00:00-24:00 session; narrowing either
        // one is what turns it into "Weeks" or "Sessions".
        private bool _restrictEpisodeTiming;
        private readonly HashSet<DayOfWeek> _allowedWeekdays = [.. Enum.GetValues<DayOfWeek>()];
        private TimeSpan _sessionStart = TimeSpan.Zero;
        private TimeSpan _sessionEnd = TimeSpan.FromHours(24);

        private static readonly DayOfWeek[] WeekdayDisplayOrder =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        ];

        private double _startingCash = 10_000;
        private double _leverage = 1;
        private double _commissionRatePercent = 0;
        private double _stopOutLevelPercent = 50;

        private readonly HashSet<string> _selectedRiskPolicies = new();

        private string? _startError;

        private string _saveName = "";

        private bool _showSaveInput;

        private bool _controlsCollapsed;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private void HandleBrainCreated(string name) => _brainName = name;

        // Feeds TrainingPreview -- whatever's currently held in the Build
        // panel's fields, live, whether or not it's been saved (see
        // BrainBuilder.CurrentConfigRecipe) -- so the preview never lags
        // behind an unsaved tweak.
        private BrainRecipe? CurrentBrainRecipe => _brainBuilder?.CurrentConfigRecipe;

        // Feeds TrainingPreview's Data column -- mirrors the Data Range card's TickDataSource fields.
        private IReadOnlyList<string> PreviewDataItems =>
        [
            $"Symbol: {(string.IsNullOrEmpty(_table) ? "(none)" : _table)}",
            $"From: {_from:yyyy-MM-dd HH:mm}",
            $"To: {_to:yyyy-MM-dd HH:mm}",
        ];

        // Feeds TrainingPreview's Training column -- mirrors the Episode Timing card's fields.
        private IReadOnlyList<string> PreviewTrainingItems =>
        [
            $"Episodes: {_episodeCount}",
            $"Bars/episode: {EffectiveEpisodeBars}",
            $"Bar width: {BarSizeOptions.Label(_barWidthSeconds)}",
            $"Timing: {EpisodeTimingSummary}",
        ];

        // When Episode Timing is restricted, Bars/episode is derived from the
        // session length / bar width rather than picked independently -- an
        // episode's decisions are meant to span exactly the configured day/session
        // (see RlTrainingService.RandomConstrainedEpisodeWindow), not some shorter
        // random slice inside it. 0 (not clamped to 1) when the session is too
        // short for even one bar, so ValidateCommonStartParams's "at least 1"
        // check still catches it instead of silently rounding up.
        private int EffectiveEpisodeBars => _restrictEpisodeTiming
            ? (int)((_sessionEnd - _sessionStart).TotalSeconds / _barWidthSeconds)
            : _episodeBars;

        // "unrestricted", or e.g. "Mo,Tu,We,Th,Fr 13:00-17:00" -- read by
        // PreviewTrainingItems above, same weekday/session state the Episode
        // Timing card's controls edit.
        private string EpisodeTimingSummary
        {
            get
            {
                if (!_restrictEpisodeTiming)
                    return "unrestricted";
                string weekdays = string.Join(",", WeekdayDisplayOrder.Where(_allowedWeekdays.Contains).Select(WeekdayAbbreviation));
                return $"{weekdays} {SessionStartText}-{SessionEndText}";
            }
        }

        private void ToggleRestrictEpisodeTiming(ChangeEventArgs e) => _restrictEpisodeTiming = (bool)e.Value!;

        private void ToggleWeekday(DayOfWeek day)
        {
            if (!_allowedWeekdays.Remove(day))
                _allowedWeekdays.Add(day);
        }

        private static string WeekdayAbbreviation(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "Mo",
            DayOfWeek.Tuesday => "Tu",
            DayOfWeek.Wednesday => "We",
            DayOfWeek.Thursday => "Th",
            DayOfWeek.Friday => "Fr",
            DayOfWeek.Saturday => "Sa",
            DayOfWeek.Sunday => "Su",
            _ => day.ToString(),
        };

        // "HH:mm" (plus the literal "24:00" for end-of-day) <-> TimeSpan bridge for
        // the session range's plain-text inputs -- a native <input type="time">
        // can't represent 24:00, which the full-day default needs, so this uses
        // text inputs instead. Invalid text is silently ignored (keeps the last
        // valid value) rather than surfacing a parse error on every keystroke.
        private string SessionStartText
        {
            get => EpisodeTimeConstraint.FormatClock(_sessionStart);
            set { if (TimeSpan.TryParse(value, out var parsed)) _sessionStart = parsed; }
        }

        private string SessionEndText
        {
            get => EpisodeTimeConstraint.FormatClock(_sessionEnd);
            set
            {
                if (value.Trim() is "24:00" or "24:00:00" or "24")
                { _sessionEnd = TimeSpan.FromHours(24); return; }
                if (TimeSpan.TryParse(value, out var parsed))
                    _sessionEnd = parsed;
            }
        }

        // Feeds TryStartTraining -- EpisodeTimeConstraint.None (unrestricted,
        // every weekday/full session) unless the Episode Timing card's toggle is on.
        private EpisodeTimeConstraint CurrentEpisodeTimeConstraint => _restrictEpisodeTiming
            ? new EpisodeTimeConstraint(true, _allowedWeekdays, _sessionStart, _sessionEnd)
            : EpisodeTimeConstraint.None;

        private void ToggleControls() => _controlsCollapsed = !_controlsCollapsed;

        private bool _dashboardView;

        private void ToggleDashboardView() => _dashboardView = !_dashboardView;

        private TimeSpan EpisodeBlockSpan => TimeSpan.FromSeconds(_barWidthSeconds) * EffectiveEpisodeBars;

        private int ProgressPercent =>
            Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);

        private TrainedModelSummary? CurrentActiveSnapshot =>
            string.IsNullOrWhiteSpace(_brainName) ? null : Service.GetActiveSnapshot(_brainName);

        private bool ValidateCommonStartParams()
        {
            if (string.IsNullOrWhiteSpace(_brainName))
            { _startError = "pick a brain"; return false; }
            if (string.IsNullOrWhiteSpace(_strategyName))
            { _startError = "pick a strategy"; return false; }
            if (string.IsNullOrWhiteSpace(_rewardPolicyName))
            { _startError = "pick a reward policy"; return false; }
            if (_episodeCount < 1)
            { _startError = "episode count must be positive"; return false; }
            if (EffectiveEpisodeBars < 1)
            { _startError = _restrictEpisodeTiming ? "session is too short for even one bar at this bar width" : "bars per episode must be at least 1"; return false; }
            if (_startingCash <= 0)
            { _startError = "starting cash must be positive"; return false; }
            if (_learningRate <= 0)
            { _startError = "learning rate must be positive"; return false; }
            if (_leverage < 1)
            { _startError = "leverage must be at least 1"; return false; }
            if (_commissionRatePercent < 0)
            { _startError = "commission rate can't be negative"; return false; }
            if (_stopOutLevelPercent <= 0 || _stopOutLevelPercent > 100)
            { _startError = "stop-out level must be between 0 and 100%"; return false; }
            return true;
        }

        private TraderSettings CurrentTraderSettings => new(
            _leverage, _stopOutLevelPercent, new PercentOfNotionalCommission(_commissionRatePercent),
            _selectedRiskPolicies.Count > 0 ? _selectedRiskPolicies.ToList() : null);

        private void ToggleRiskPolicy(string policyName, bool selected)
        {
            if (selected)
                _selectedRiskPolicies.Add(policyName);
            else
                _selectedRiskPolicies.Remove(policyName);
        }

        private void StartTraining()
        {
            StartTrainingCore(pauseAfterFirstEpisode: false);
            if (_startError is null)
                _controlsCollapsed = true;
        }

        private void StepTraining()
        {
            if (Service.IsRunning)
            { Service.StepTraining(); return; }
            StartTrainingCore(pauseAfterFirstEpisode: true);
        }

        private void StartTrainingCore(bool pauseAfterFirstEpisode)
        {
            _startError = null;

            // The Build panel's fields are always the source of truth for
            // what trains, regardless of whether the panel is expanded. If
            // they don't match what's saved under their name, this drafts
            // (or refreshes) a live, unsaved brain from them, so tweaking
            // parameters between runs never needs saving first (see
            // BrainBuilder.PrepareBrainForTraining). If they already match,
            // this just echoes _brainName back untouched, so any weights
            // already trained this session keep going.
            var brainName = _brainBuilder?.PrepareBrainForTraining();
            if (_brainBuilder is not null && brainName is null)
            { _startError = "fix the build panel before starting training"; return; }
            if (brainName is not null)
                _brainName = brainName;

            if (!ValidateCommonStartParams())
                return;
            if (string.IsNullOrWhiteSpace(_table))
            { _startError = "pick a symbol"; return; }
            if (_to <= _from)
            { _startError = "To must be after From"; return; }
            if (_restrictEpisodeTiming && _allowedWeekdays.Count == 0)
            { _startError = "episode timing: pick at least one weekday"; return; }
            if (_restrictEpisodeTiming && _sessionEnd <= _sessionStart)
            { _startError = "episode timing: session end must be after session start"; return; }

            if (!Service.TryStartTraining(
                    _brainName, _strategyName, _rewardPolicyName, _episodeCount, EffectiveEpisodeBars, _barWidthSeconds, _startingCash, _learningRate,
                    (_table, _from, _to), CurrentTraderSettings, pauseAfterFirstEpisode, CurrentEpisodeTimeConstraint))
                _startError = Service.Error ?? "a training run is already in progress";
        }

        private void PauseTraining() => Service.PauseTraining();

        private void ResumeTraining() => Service.ResumeTraining();

        private void StopTraining() => Service.StopTraining();

        // Only reachable once training's finished/stopped (see the Reset button's
        // guard in the .razor) -- clears this run's episodes/metrics/charts back
        // to a blank slate, leaving Run Setup (Data Range/Brain/Training/Trader)
        // untouched so the same config can be run again.
        private void ResetRun() => Service.ResetRun();

        private void SaveModel()
        {
            if (string.IsNullOrWhiteSpace(_saveName))
                return;

            if (Service.SaveBrain(_saveName, _brainName, _strategyName, _rewardPolicyName, CurrentDatasetDescription,
                    EffectiveEpisodeBars, _barWidthSeconds, _startingCash, _learningRate,
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
            if (e.Key == "Enter")
                SaveModel();
            else if (e.Key == "Escape")
                CancelSaveModel();
        }

        private string CurrentDatasetDescription => $"{_table} ({_from:yyyy-MM-dd HH:mm} to {_to:yyyy-MM-dd HH:mm})";

        private double WinRatePercent
        {
            get
            {
                var episodes = Service.RecentEpisodes;
                return episodes.Count == 0 ? 0 : episodes.Count(e => e.Reward > 0) * 100.0 / episodes.Count;
            }
        }

        private double RewardSpread => Service.BestReward - Service.WorstReward;

        private (double StdDev, double Median) RewardDistribution
        {
            get
            {
                var episodes = Service.RecentEpisodes;
                return episodes.Count == 0
                    ? (0, 0)
                    : (RlEpisodeMetricStats.StdDev(episodes), RlEpisodeMetricStats.Median(episodes));
            }
        }

    }
}
