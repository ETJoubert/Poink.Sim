using System;
using System.Collections.Generic;
using Poink.Sim.Training;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>A saved model's listing row -- everything except its config and weights.</summary>
	public sealed record TrainedModelSummary(Guid Id, string Name, int EpisodesTrained, long WeightCount, DateTime CreatedAt, DateTime? UpdatedAt);

	/// <summary>A saved model as stored: its options as JSON and its weights as serialized bytes.</summary>
	internal sealed record TrainedModelData(TrainedModelSummary Summary, string ConfigJson, byte[] Weights);

	/// <summary>One saved run's listing row -- everything except its options and per-episode metrics.</summary>
	public sealed record TrainingHistorySummary(
		Guid Id,
		Guid TrainedModelId,
		string Name,
		int EpisodesCompleted,
		int ModelEpisodesTrained,
		double RecentAverageReward,
		double BestReward,
		double WorstReward,
		DateTime CreatedAt
	);

	/// <summary>A saved run as stored: its options as JSON and its per-episode metrics.</summary>
	internal sealed record TrainingHistoryData(TrainingHistorySummary Summary, string ConfigJson, IReadOnlyList<RlEpisodeMetric> Episodes);

	/// <summary>
	/// The Run tab's results at the moment of saving. EpisodesCompleted counts this session's episodes
	/// only; ModelEpisodesTrained also includes any episodes the loaded model had already trained.
	/// </summary>
	internal sealed record TrainingRunSnapshot(
		IReadOnlyList<RlEpisodeMetric> Episodes,
		int EpisodesCompleted,
		int ModelEpisodesTrained,
		double RecentAverageReward,
		double BestReward,
		double WorstReward
	);
}
