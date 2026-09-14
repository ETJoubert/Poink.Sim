using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Poink.Sim.Data.Entities
{
	/// <summary>
	/// A closed-position deal persisted from cTrader's account history, correlated to the account/position/symbol
	/// by their raw cTrader ids rather than a DB-enforced foreign key, matching how the rest of the app correlates
	/// cTrader data.
	/// </summary>
	[Index(nameof(DealId), IsUnique = true)]
	[Index(nameof(AccountId), nameof(ExecutionTimeUtc))]
	public class TradeDeal
	{
		[Key]
		public Guid Id { get; set; }

		public long AccountId { get; set; }

		public long DealId { get; set; }

		public long PositionId { get; set; }

		public long SymbolId { get; set; }

		public string SymbolName { get; set; } = string.Empty;

		public bool IsLong { get; set; }

		public double Volume { get; set; }

		public double ExecutionPrice { get; set; }

		public DateTime ExecutionTimeUtc { get; set; }

		public double? RealizedPnl { get; set; }

		public double? Commission { get; set; }

		public double? Swap { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
