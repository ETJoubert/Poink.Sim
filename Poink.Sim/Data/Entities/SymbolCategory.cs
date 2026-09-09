using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Poink.Sim.Models;

namespace Poink.Sim.Data.Entities
{
	/// <summary>
	/// One row per cTrader ProtoOASymbolCategory, correlated by <see cref="SymbolCategoryId"/> rather than
	/// a DB-enforced foreign key, matching how <see cref="SymbolMetadata"/> correlates to <see cref="Symbol"/>.
	/// </summary>
	[Index(nameof(SymbolCategoryId), IsUnique = true)]
	public class SymbolCategory
	{
		[Key]
		public Guid Id { get; set; }

		public long SymbolCategoryId { get; set; }

		public string Name { get; set; } = string.Empty;

		public long AssetClassId { get; set; }

		public string AssetClassName { get; set; } = string.Empty;

		public SymbolCategoryBucket Bucket { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? ChangedAt { get; set; }
	}
}
