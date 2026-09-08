using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Poink.Sim.Data.Entities
{
	[Index(nameof(SymbolId), IsUnique = true)]
	public class Symbol
	{
		[Key]
		public Guid Id { get; set; }

		public long SymbolId { get; set; }

		public string SymbolName { get; set; } = string.Empty;

		public bool Enabled { get; set; }

		public long BaseAssetId { get; set; }

		public long QuoteAssetId { get; set; }

		public long SymbolCategoryId { get; set; }

		public string Description { get; set; } = string.Empty;

		public double SortingNumber { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? ChangedAt { get; set; }
	}
}
