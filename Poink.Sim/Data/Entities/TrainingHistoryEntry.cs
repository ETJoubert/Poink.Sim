using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Poink.Sim.Data.Entities
{
	/// <summary>One saved set of training results for a <see cref="TrainedModel"/>: the run's per-episode metrics and summary stats, with the options it ran under. Deleted with its model.</summary>
	public class TrainingHistoryEntry
	{
		[Key]
		public Guid Id { get; set; }

		public Guid TrainedModelId { get; set; }

		[ForeignKey(nameof(TrainedModelId))]
		public TrainedModel TrainedModel { get; set; } = null!;

		public string Name { get; set; } = string.Empty;

		[Column(TypeName = "jsonb")]
		public string ConfigJson { get; set; } = string.Empty;

		[Column(TypeName = "jsonb")]
		public string EpisodeMetricsJson { get; set; } = string.Empty;

		public int EpisodesCompleted { get; set; }

		public int ModelEpisodesTrained { get; set; }

		public double RecentAverageReward { get; set; }

		public double BestReward { get; set; }

		public double WorstReward { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
