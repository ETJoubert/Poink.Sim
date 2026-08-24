using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Poink.Sim.Data.Entities
{
	public class TrainingResult
	{
		[Key]
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		[Column(TypeName = "jsonb")]
		public string ConfigJson { get; set; } = string.Empty;

		[Column(TypeName = "jsonb")]
		public string StatsJson { get; set; } = string.Empty;

		[Column(TypeName = "jsonb")]
		public string GraphJson { get; set; } = string.Empty;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
