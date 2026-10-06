namespace Poink.Sim.Components.Charts
{
	/// <summary>How <see cref="CompareTxFold"/> places each timeframe's symbol bars.</summary>
	public enum CompareTxFoldLayout
	{
		/// <summary>Every symbol's bar drawn on the same spot at partial opacity.</summary>
		Overlaid,

		/// <summary>Every symbol's bar in its own slot within the timeframe's group.</summary>
		SideBySide,
	}

	/// <summary>
	/// One symbol on a <see cref="CompareTxFold"/>: the tick table it reads, the theme colour var it is drawn in, and an
	/// optional table supplying its ticks before <see cref="CompareTxFold.HistoryBeforeUtc"/>.
	/// </summary>
	public sealed record CompareTxFoldSymbol(string Table, string ColorVar, string? HistoryTable = null);
}
