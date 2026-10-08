using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Decides which of an armed side's positions are back to entry or better and should close now.</summary>
	public static class BreakevenCloseHelper
	{
		/// <summary>
		/// A long closes against the bid and a short against the ask (as cTrader marks them), so each is at breakeven or
		/// better once that price is back to its entry.
		/// </summary>
		public static bool IsAtOrPastEntry(CTraderPositionInfo position, double bid, double ask) =>
			position.IsLong ? bid >= position.EntryPrice : ask <= position.EntryPrice;

		public static IReadOnlyList<BreakevenArmedEntry> ToEntries(long accountId, IEnumerable<BreakevenSide> sides) =>
			sides.Select(s => new BreakevenArmedEntry(accountId, s.SymbolId, s.IsLong)).ToList();

		/// <summary>The stored sides to re-arm: only those armed on <paramref name="accountId"/>, so a switched account is never touched.</summary>
		public static IReadOnlyList<BreakevenSide> SidesToRearm(IEnumerable<BreakevenArmedEntry>? entries, long accountId) =>
			(entries ?? []).Where(e => e.AccountId == accountId).Select(e => new BreakevenSide(e.SymbolId, e.IsLong)).Distinct().ToList();

		public static IReadOnlyList<CTraderPositionInfo> SidePositions(IEnumerable<CTraderPositionInfo> positions, BreakevenSide side) =>
			positions.Where(p => p.SymbolId == side.SymbolId && p.IsLong == side.IsLong).ToList();

		/// <summary>The side's positions at breakeven or better that are not already being closed.</summary>
		public static IReadOnlyList<CTraderPositionInfo> DuePositions(
			IEnumerable<CTraderPositionInfo> positions,
			BreakevenSide side,
			double bid,
			double ask,
			IReadOnlySet<long> closingPositionIds
		) =>
			SidePositions(positions, side)
				.Where(p => !closingPositionIds.Contains(p.PositionId) && IsAtOrPastEntry(p, bid, ask))
				.ToList();
	}
}
