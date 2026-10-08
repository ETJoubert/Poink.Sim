namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>One side of one symbol whose open positions close as each gets back to its entry price or better.</summary>
	public readonly record struct BreakevenSide(long SymbolId, bool IsLong)
	{
		public string Name => IsLong ? "long" : "short";
	}

	/// <summary>An armed side as kept in the tab's session storage, tied to the account it was armed on.</summary>
	public sealed record BreakevenArmedEntry(long AccountId, long SymbolId, bool IsLong);

	/// <summary>
	/// The quick console's automatic close helpers: with <see cref="AutoArm"/> each position joins the breakeven watch once
	/// it has been open <see cref="ArmAfterSeconds"/> (t); with <see cref="AutoClose"/> it closes at market once it has been
	/// open t + <see cref="CloseAfterSeconds"/> (s), whatever its P&amp;L.
	/// </summary>
	public sealed record CloseAutomationSettings(bool AutoArm, int ArmAfterSeconds, bool AutoClose, int CloseAfterSeconds)
	{
		public int TimeStopSeconds => ArmAfterSeconds + CloseAfterSeconds;

		public bool AnyEnabled => AutoArm || AutoClose;
	}

	/// <summary>The close automation and the one symbol (the charted one) whose positions it manages.</summary>
	public sealed record CloseAutomationScope(long SymbolId, CloseAutomationSettings Settings);

	/// <summary>How far a position is through one of the close automation's delays, clamped to that delay.</summary>
	public sealed record CloseCountdown(double ElapsedSeconds, int TotalSeconds)
	{
		public double Fraction => TotalSeconds > 0 ? ElapsedSeconds / TotalSeconds : 1;

		public double RemainingSeconds => TotalSeconds - ElapsedSeconds;

		public bool IsDone => ElapsedSeconds >= TotalSeconds;
	}

	/// <summary>Why a position is due to close now, if at all.</summary>
	public enum PositionCloseReason
	{
		None,
		Breakeven,
		TimeStop,
	}
}
