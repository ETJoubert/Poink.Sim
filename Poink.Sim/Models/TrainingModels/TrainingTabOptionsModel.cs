using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>Which slice of the Data tab's selected range each episode is fetched from.</summary>
	internal enum EpisodeSamplingMode
	{
		RandomFromSet,
		EntireSet,
		WindowSelected,
	}

	/// <summary>
	/// How episodes are timed/sampled. AllowedWeekdays null means every day is allowed; SessionEnd
	/// null means the session runs to end of day (24:00). TrainingBars/VisibleBars/RestrictEpisodeTiming
	/// only apply to <see cref="EpisodeSamplingMode.RandomFromSet"/> -- the other modes fetch an exact,
	/// already-known range instead of sizing/positioning a random one.
	/// </summary>
	internal sealed record EpisodeTimingOptionsModel(
		int EpisodeCount = 1000,
		int TrainingBars = 20,
		int VisibleBars = 120,
		bool RestrictEpisodeTiming = false,
		IReadOnlyList<DayOfWeek>? AllowedWeekdays = null,
		TimeSpan SessionStart = default,
		TimeSpan? SessionEnd = null,
		EpisodeSamplingMode SamplingMode = EpisodeSamplingMode.RandomFromSet,
		DateTime? WindowStart = null,
		DateTime? WindowEnd = null
	);

	/// <summary>The trader each episode starts with.</summary>
	internal sealed record TraderSetupOptionsModel(
		double StartingCash = 10_000,
		double Leverage = 1,
		double CommissionRatePercent = 0,
		double StopOutLevelPercent = 50,
		IReadOnlyList<string>? SelectedRiskPolicies = null,
		PositionControlOptionsModel? Control = null
	)
	{
		public PositionControlOptionsModel EffectiveControl => Control ?? new PositionControlOptionsModel();
	}

	/// <summary>Which training strategy and reward policy drive learning, at what rate, and whether
	/// an entropy bonus encourages exploration (anneals to 0 over the run -- see PolicyGradientLearner).</summary>
	internal sealed record LearningOptionsModel(
		string TrainingStrategyName = TrainingStrategyCatalog.ReinforceName,
		string RewardPolicyName = "Equity Drift (default)",
		double LearningRate = 0.001,
		bool UseEntropyBonus = true
	);

	/// <summary>
	/// Gates whether the network is even asked to decide, as opposed to Output tab's Action Masking
	/// which restricts choices afterward.
	/// </summary>
	internal sealed record DecisionMaskingOptionsModel(
		DecisionMaskResolutionKind DecisionMaskResolution = DecisionMaskResolutionKind.PerBar,
		double DecisionMaskResolutionSeconds = 1.0,
		double DecisionMaskResolutionCellWidth = 0.10,
		bool MaskDecisionPriceBelowPoc = false
	);

	/// <summary>The Training tab's episode timing, trader setup, learning, and decision masking settings.</summary>
	internal sealed record TrainingTabOptionsModel(
		EpisodeTimingOptionsModel EpisodeTiming,
		TraderSetupOptionsModel TraderSetup,
		LearningOptionsModel Learning,
		DecisionMaskingOptionsModel DecisionMasking
	);
}
