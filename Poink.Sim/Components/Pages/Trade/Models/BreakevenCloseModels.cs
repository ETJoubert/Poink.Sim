namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>One side of one symbol whose open positions close as each gets back to its entry price or better.</summary>
	public readonly record struct BreakevenSide(long SymbolId, bool IsLong)
	{
		public string Name => IsLong ? "long" : "short";
	}

	/// <summary>An armed side as kept in the tab's session storage, tied to the account it was armed on.</summary>
	public sealed record BreakevenArmedEntry(long AccountId, long SymbolId, bool IsLong);
}
