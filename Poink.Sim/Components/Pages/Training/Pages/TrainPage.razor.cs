using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Training;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
	public partial class TrainPage : ComponentBase, IDisposable
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Inject]
		private IRiskPolicyService RiskPolicyService { get; set; } = default!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		[Inject]
		private ITrainingResultService TrainingResultService { get; set; } = default!;

		// Set via @ref; StartTrainingCore asks it to draft a live, unsaved brain from its
		// current fields right before a run starts (see BrainBuilder.PrepareBrainForTraining).
		private BrainBuilder? _brainBuilder;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;

		private string _brainName = "";

		private int _barWidthSeconds = 60;
		private SimulationSteppingMode _steppingMode = SimulationSteppingMode.Bar;
		private double _cellWidth = 0.10;

		private string _strategyName = "";
		private int _episodeCount = 1000;
		private int _episodeBars = 20;
		private double _learningRate = 0.001;

		// Off by default, so an episode may start anywhere in the Data Range above. Weekday and
		// session are independent, combinable filters -- see EpisodeTimeConstraint's doc comment.
		private bool _restrictEpisodeTiming;
		private readonly HashSet<DayOfWeek> _allowedWeekdays = [.. Enum.GetValues<DayOfWeek>()];
		private TimeSpan _sessionStart = TimeSpan.Zero;
		private TimeSpan _sessionEnd = TimeSpan.FromHours(24);

		private static readonly DayOfWeek[] WeekdayDisplayOrder =
		[
			DayOfWeek.Monday,
			DayOfWeek.Tuesday,
			DayOfWeek.Wednesday,
			DayOfWeek.Thursday,
			DayOfWeek.Friday,
			DayOfWeek.Saturday,
			DayOfWeek.Sunday,
		];

		private double _startingCash = 10_000;
		private double _leverage = 1;
		private double _commissionRatePercent = 0;
		private double _stopOutLevelPercent = 50;

		private readonly List<string> _selectedRiskPolicies = new();

		private string? _startError;

		private string _saveName = "";

		private bool _showSaveInput;

		private bool _controlsCollapsed;

		private List<TrainingResultSummary> _savedResults = [];
		private string _saveResultsName = "";
		private bool _showSaveResultsInput;
		private string? _resultsStatus;

		private enum TrainTab
		{
			Live,
			SavedResults,
		}

		private TrainTab _activeTab;

		protected override void OnInitialized()
		{
			Service.OnChanged += HandleServiceChanged;
		}

		protected override async Task OnInitializedAsync() => await RefreshSavedResultsAsync();

		private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

		public void Dispose() => Service.OnChanged -= HandleServiceChanged;

		private void HandleBrainCreated(string name) => _brainName = name;

		// Round-trips through localStorage since this component's own fields don't survive
		// navigation; persisting is diffed against _lastPersistedSettingsJson to avoid write spam.
		private const string SettingsStorageKey = "poink.train.runSetup";

		private string? _lastPersistedSettingsJson;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				await LoadPersistedSettingsAsync();
				return;
			}

			await PersistSettingsIfChangedAsync();
		}

		private TrainPageSettings CurrentSettings =>
			new(
				_table,
				_from,
				_to,
				_brainName,
				_strategyName,
				_learningRate,
				_barWidthSeconds,
				_steppingMode,
				_cellWidth,
				_episodeCount,
				_episodeBars,
				_restrictEpisodeTiming,
				[.. _allowedWeekdays],
				_sessionStart,
				_sessionEnd,
				_startingCash,
				_leverage,
				_commissionRatePercent,
				_stopOutLevelPercent,
				[.. _selectedRiskPolicies]
			);

		private async Task LoadPersistedSettingsAsync()
		{
			var settings = await LocalStorage.GetItemAsync<TrainPageSettings>(SettingsStorageKey);
			if (settings is null)
				return;

			ApplySettings(settings);
			_lastPersistedSettingsJson = JsonSerializer.Serialize(CurrentSettings);
			StateHasChanged();
		}

		private void ApplySettings(TrainPageSettings settings)
		{
			_table = settings.Table;
			_from = settings.From;
			_to = settings.To;
			_brainName = settings.BrainName;
			_strategyName = settings.StrategyName;
			_learningRate = settings.LearningRate;
			_barWidthSeconds = settings.BarWidthSeconds;
			_steppingMode = settings.SteppingMode;
			_cellWidth = settings.CellWidth;
			_episodeCount = settings.EpisodeCount;
			_episodeBars = settings.EpisodeBars;
			_restrictEpisodeTiming = settings.RestrictEpisodeTiming;
			_allowedWeekdays.Clear();
			foreach (var day in settings.AllowedWeekdays)
				_allowedWeekdays.Add(day);
			_sessionStart = settings.SessionStart;
			_sessionEnd = settings.SessionEnd;
			_startingCash = settings.StartingCash;
			_leverage = settings.Leverage;
			_commissionRatePercent = settings.CommissionRatePercent;
			_stopOutLevelPercent = settings.StopOutLevelPercent;
			_selectedRiskPolicies.Clear();
			_selectedRiskPolicies.AddRange(settings.SelectedRiskPolicies);
		}

		private async Task PersistSettingsIfChangedAsync()
		{
			var json = JsonSerializer.Serialize(CurrentSettings);
			if (json == _lastPersistedSettingsJson)
				return;

			_lastPersistedSettingsJson = json;
			await LocalStorage.SetItemAsync(SettingsStorageKey, CurrentSettings);
		}

		// Feeds TrainingPreview from whatever's live in the Build panel's fields, whether or
		// not it's been saved, so the preview never lags behind an unsaved tweak.
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

		// When restricted, Bars/episode derives from session length / bar width; 0 (not 1) when
		// too short for a bar, so ValidateCommonStartParams's "at least 1" check still catches it.
		private int EffectiveEpisodeBars =>
			_restrictEpisodeTiming
				? (int)((_sessionEnd - _sessionStart).TotalSeconds / _barWidthSeconds)
				: _episodeBars;

		// "unrestricted", or e.g. "Mo,Tu,We,Th,Fr 13:00-17:00", read by PreviewTrainingItems.
		private string EpisodeTimingSummary
		{
			get
			{
				if (!_restrictEpisodeTiming)
					return "unrestricted";
				string weekdays = string.Join(
					",",
					WeekdayDisplayOrder.Where(_allowedWeekdays.Contains).Select(WeekdayAbbreviation)
				);
				return $"{weekdays} {SessionStartText}-{SessionEndText}";
			}
		}

		private void ToggleRestrictEpisodeTiming(ChangeEventArgs e) => _restrictEpisodeTiming = (bool)e.Value!;

		private void ToggleWeekday(DayOfWeek day)
		{
			if (!_allowedWeekdays.Remove(day))
				_allowedWeekdays.Add(day);
		}

		private static string WeekdayAbbreviation(DayOfWeek day) =>
			day switch
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

		// Plain-text "HH:mm" (plus literal "24:00") bridge, since a native <input type="time">
		// can't represent the full-day default; invalid text is silently ignored, not parsed.
		private string SessionStartText
		{
			get => EpisodeTimeConstraint.FormatClock(_sessionStart);
			set
			{
				if (TimeSpan.TryParse(value, out var parsed))
					_sessionStart = parsed;
			}
		}

		private string SessionEndText
		{
			get => EpisodeTimeConstraint.FormatClock(_sessionEnd);
			set
			{
				if (value.Trim() is "24:00" or "24:00:00" or "24")
				{
					_sessionEnd = TimeSpan.FromHours(24);
					return;
				}
				if (TimeSpan.TryParse(value, out var parsed))
					_sessionEnd = parsed;
			}
		}

		// Feeds TryStartTraining -- EpisodeTimeConstraint.None (unrestricted,
		// every weekday/full session) unless the Episode Timing card's toggle is on.
		private EpisodeTimeConstraint CurrentEpisodeTimeConstraint =>
			_restrictEpisodeTiming
				? new EpisodeTimeConstraint(true, _allowedWeekdays, _sessionStart, _sessionEnd)
				: EpisodeTimeConstraint.None;

		private void ToggleControls() => _controlsCollapsed = !_controlsCollapsed;

		private TimeSpan EpisodeBlockSpan => TimeSpan.FromSeconds(_barWidthSeconds) * EffectiveEpisodeBars;

		private int ProgressPercent =>
			Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);

		private NeuralWeightsSummary? CurrentActiveSnapshot =>
			string.IsNullOrWhiteSpace(_brainName) ? null : Service.GetActiveSnapshot(_brainName);

		private bool ValidateCommonStartParams()
		{
			if (string.IsNullOrWhiteSpace(_brainName))
			{
				_startError = "pick a brain";
				return false;
			}
			if (string.IsNullOrWhiteSpace(_strategyName))
			{
				_startError = "pick a strategy";
				return false;
			}
			if (_episodeCount < 1)
			{
				_startError = "episode count must be positive";
				return false;
			}
			if (EffectiveEpisodeBars < 1)
			{
				_startError = _restrictEpisodeTiming
					? "session is too short for even one bar at this bar width"
					: "bars per episode must be at least 1";
				return false;
			}
			if (_startingCash <= 0)
			{
				_startError = "starting cash must be positive";
				return false;
			}
			if (_learningRate <= 0)
			{
				_startError = "learning rate must be positive";
				return false;
			}
			if (_leverage < 1)
			{
				_startError = "leverage must be at least 1";
				return false;
			}
			if (_commissionRatePercent < 0)
			{
				_startError = "commission rate can't be negative";
				return false;
			}
			if (_stopOutLevelPercent <= 0 || _stopOutLevelPercent > 100)
			{
				_startError = "stop-out level must be between 0 and 100%";
				return false;
			}
			return true;
		}

		private TraderSettings CurrentTraderSettings =>
			new(
				_leverage,
				_stopOutLevelPercent,
				new PercentOfNotionalCommission(_commissionRatePercent),
				_selectedRiskPolicies.Count > 0 ? _selectedRiskPolicies.ToList() : null
			);

		private void StartTraining()
		{
			StartTrainingCore(pauseAfterFirstEpisode: false);
			if (_startError is null)
				_controlsCollapsed = true;
		}

		private void StepTraining()
		{
			if (Service.IsRunning)
			{
				Service.StepTraining();
				return;
			}
			StartTrainingCore(pauseAfterFirstEpisode: true);
		}

		private void StartTrainingCore(bool pauseAfterFirstEpisode)
		{
			_startError = null;

			// While the Build panel is open, unsaved edits are drafted live so tweaks never
			// need saving first (see BrainBuilder.PrepareBrainForTraining); closed, it trains the brain selected above as-is.
			var brainName = _brainBuilder?.PrepareBrainForTraining();
			if (_brainBuilder is not null && brainName is null)
			{
				_startError = "fix the build panel before starting training";
				return;
			}
			if (brainName is not null)
				_brainName = brainName;

			if (!ValidateCommonStartParams())
				return;
			if (string.IsNullOrWhiteSpace(_table))
			{
				_startError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_startError = "To must be after From";
				return;
			}
			if (_restrictEpisodeTiming && _allowedWeekdays.Count == 0)
			{
				_startError = "episode timing: pick at least one weekday";
				return;
			}
			if (_restrictEpisodeTiming && _sessionEnd <= _sessionStart)
			{
				_startError = "episode timing: session end must be after session start";
				return;
			}

			var startRequest = new TrainingStartRequest(
				_brainName,
				_strategyName,
				_episodeCount,
				EffectiveEpisodeBars,
				_barWidthSeconds,
				_startingCash,
				_learningRate,
				(_table, _from, _to),
				CurrentTraderSettings,
				pauseAfterFirstEpisode,
				CurrentEpisodeTimeConstraint,
				_steppingMode,
				_cellWidth
			);
			if (!Service.TryStartTraining(startRequest))
				_startError = Service.Error ?? "a training run is already in progress";
		}

		private void PauseTraining() => Service.PauseTraining();

		private void ResumeTraining() => Service.ResumeTraining();

		private void StopTraining() => Service.StopTraining();

		// Clears this run's episodes/metrics/charts, leaving Run Setup untouched so the same
		// config can be run again; only reachable once training's finished/stopped.
		private void ResetRun() => Service.ResetRun();

		private void SaveModel()
		{
			if (string.IsNullOrWhiteSpace(_saveName))
				return;

			var saveRequest = new TrainingSaveRequest(
				_saveName,
				_brainName,
				_strategyName,
				CurrentDatasetDescription,
				EffectiveEpisodeBars,
				_barWidthSeconds,
				_startingCash,
				_learningRate,
				CurrentTraderSettings
			);
			if (Service.SaveTrainedBrain(saveRequest))
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

		// Mirrors CurrentSettings' fields -- a decoupled snapshot for a saved
		// TrainingResult rather than the page's own round-tripped UI state.
		private TrainingConfig CurrentTrainingConfig =>
			new(
				_table,
				_from,
				_to,
				_brainName,
				_strategyName,
				_learningRate,
				_barWidthSeconds,
				_steppingMode,
				_cellWidth,
				_episodeCount,
				_episodeBars,
				_restrictEpisodeTiming,
				[.. _allowedWeekdays],
				_sessionStart,
				_sessionEnd,
				_startingCash,
				_leverage,
				_commissionRatePercent,
				_stopOutLevelPercent,
				[.. _selectedRiskPolicies]
			);

		private TrainingSessionStats CurrentTrainingSessionStats =>
			new(
				Service.EpisodesCompleted,
				Service.TotalEpisodes,
				Service.RecentAverageReward,
				Service.BestReward,
				Service.WorstReward,
				Service.EpisodesPerSecond,
				Service.ActionCounts,
				WinRatePercent,
				RewardSpread,
				RewardDistribution.StdDev,
				RewardDistribution.Median
			);

		private TrainingResultsGraph CurrentTrainingResultsGraph =>
			new(Service.RecentEpisodes, Service.RecentEpisodeWindows);

		private bool CanSaveResults => Service.RecentEpisodes.Count > 0;

		private void ShowSaveResultsInput() => _showSaveResultsInput = true;

		private void CancelSaveResults()
		{
			_showSaveResultsInput = false;
			_saveResultsName = "";
		}

		private async Task SaveResultsAsync()
		{
			if (string.IsNullOrWhiteSpace(_saveResultsName))
				return;

			try
			{
				await TrainingResultService.CreateAsync(
					_saveResultsName,
					CurrentTrainingConfig,
					CurrentTrainingSessionStats,
					CurrentTrainingResultsGraph
				);
				_resultsStatus = $"saved '{_saveResultsName}'";
				_saveResultsName = "";
				_showSaveResultsInput = false;
				await RefreshSavedResultsAsync();
			}
			catch (Exception ex)
			{
				_resultsStatus = $"save failed: {ex.Message}";
			}
		}

		private void OnSaveResultsNameKeyDown(KeyboardEventArgs e)
		{
			if (e.Key == "Enter")
				_ = SaveResultsAsync();
			else if (e.Key == "Escape")
				CancelSaveResults();
		}

		private async Task RefreshSavedResultsAsync() => _savedResults = [.. await TrainingResultService.ListAsync()];

		private async Task DeleteResultAsync(TrainingResultSummary summary)
		{
			await TrainingResultService.DeleteAsync(summary.Id);
			await RefreshSavedResultsAsync();
		}

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
