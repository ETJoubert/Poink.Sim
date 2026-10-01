using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Poink.Sim.Data.Entities
{
	/// <summary>A network trained by the Training Engine: the options it was built and trained with, plus its weights. Saving under an existing name overwrites that model.</summary>
	[Index(nameof(Name), IsUnique = true)]
	public class TrainedModel
	{
		[Key]
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		[Column(TypeName = "jsonb")]
		public string ConfigJson { get; set; } = string.Empty;

		public byte[] Weights { get; set; } = Array.Empty<byte>();

		public long WeightCount { get; set; }

		public int EpisodesTrained { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? UpdatedAt { get; set; }

		public ICollection<TrainingHistoryEntry> History { get; set; } = new List<TrainingHistoryEntry>();
	}
}
