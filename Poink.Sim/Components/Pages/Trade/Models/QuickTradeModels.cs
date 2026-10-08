using System;

namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>
	/// One side of the quick trade console: how much is open on the charted symbol, whether a request is in flight, and
	/// whether its positions are armed to close at breakeven.
	/// </summary>
	public sealed record QuickTradeSideState(bool IsLong, int OpenCount, double OpenVolume, bool Busy, bool BreakevenArmed = false)
	{
		public bool HasOpenPositions => OpenCount > 0;
	}

	/// <summary>
	/// A side held locked after a successful request until cTrader reports its effect: an open until the side's
	/// position count rises above <see cref="BaselineCount"/>, a close until the side has no positions left.
	/// </summary>
	public sealed record QuickTradeAwaitedReport(long SymbolId, bool IsOpening, int BaselineCount, DateTime DeadlineUtc);

	/// <summary>A market order the quick trade console asks the page to place.</summary>
	public sealed record QuickTradeOpenRequest(bool IsLong, double Volume);

	/// <summary>Which console the Trade tab shows; persisted per browser.</summary>
	public enum TradeConsoleMode
	{
		Simple,
		Full,
	}
}
