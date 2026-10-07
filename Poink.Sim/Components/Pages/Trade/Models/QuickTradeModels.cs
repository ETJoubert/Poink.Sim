namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>One side of the quick trade console: how much is open on the charted symbol and whether a request is in flight.</summary>
	public sealed record QuickTradeSideState(bool IsLong, int OpenCount, double OpenVolume, bool Busy)
	{
		public bool HasOpenPositions => OpenCount > 0;
	}

	/// <summary>A market order the quick trade console asks the page to place.</summary>
	public sealed record QuickTradeOpenRequest(bool IsLong, double Volume);

	/// <summary>Which console the Trade tab shows; persisted per browser.</summary>
	public enum TradeConsoleMode
	{
		Simple,
		Full,
	}
}
