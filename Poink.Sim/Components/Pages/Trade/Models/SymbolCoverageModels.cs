using System;

namespace Poink.Sim.Components.Pages.Trade.Models
{
	public enum SymbolCoverageStatus
	{
		None,
		Stale,
		Current,
	}

	/// <summary>A symbol's stored tick history (its live tick table), as recorded in the tick_table_stats registry.</summary>
	public sealed record SymbolTickCoverage(string Table, long Rows, DateTime? MinUtc, DateTime? MaxUtc, SymbolCoverageStatus Status)
	{
		public bool HasData => Status != SymbolCoverageStatus.None;
	}
}
