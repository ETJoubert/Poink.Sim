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
    public partial class TrainPage : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        [Inject] private NetworkService NetworkService { get; set; } = default!;

        private string _table = "";
        private DateTime _from = DateTime.UtcNow.AddDays(-1);
        private DateTime _to = DateTime.UtcNow;

        private string _brainName = "";

        private bool _showBuildBrain;

        private int _barWidthMinutes = 1;

        private string _strategyName = "";
        private string _rewardPolicyName = "";
        private int _episodeCount = 100;
        private int _episodeBars = 200;
        private double _learningRate = 0.001;

        private double _startingCash = 10_000;
        private double _leverage = 1;
        private double _commissionRatePercent = 0;
        private double _stopOutLevelPercent = 50;

        private string? _startError;

        private string _saveName = "";

        private bool _showSaveInput;

        private bool _controlsCollapsed;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (TrainableBrainOptions.Count > 0) _brainName = TrainableBrainOptions[0].Name;
            if (Service.StrategyNames.Count > 0) _strategyName = Service.StrategyNames[0];
            if (Service.RewardPolicyNames.Count > 0) _rewardPolicyName = Service.RewardPolicyNames[0];
        }

        private IReadOnlyList<BrainOption> TrainableBrainOptions =>
            NetworkService.Networks.Select(n => new BrainOption(n.Name, true)).ToList();

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        private void OnStrategyChanged(ChangeEventArgs e) => _strategyName = e.Value?.ToString() ?? "";

        private void OnRewardPolicyChanged(ChangeEventArgs e) => _rewardPolicyName = e.Value?.ToString() ?? "";

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        private void ToggleBuildBrain() => _showBuildBrain = !_showBuildBrain;

        private void HandleBrainCreated(string name)
        {
            _brainName = name;
            _showBuildBrain = false;
        }

        private void ToggleControls() => _controlsCollapsed = !_controlsCollapsed;

        private bool _dashboardView;

        private void ToggleDashboardView() => _dashboardView = !_dashboardView;

        private TimeSpan EpisodeBlockSpan => TimeSpan.FromMinutes(_barWidthMinutes) * _episodeBars;

        private int ProgressPercent =>
            Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);

        private TrainedModelSummary? CurrentActiveSnapshot =>
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

        private void StartTraining()
        {
            StartTrainingCore(pauseAfterFirstEpisode: false);
            if (_startError is null) _controlsCollapsed = true;
        }

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

        private string CurrentDatasetDescription => $"{_table} ({_from:yyyy-MM-dd HH:mm} to {_to:yyyy-MM-dd HH:mm})";
    }
}
