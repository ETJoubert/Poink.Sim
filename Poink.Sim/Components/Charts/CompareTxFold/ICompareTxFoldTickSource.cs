using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Lets a host supply <see cref="CompareTxFold"/>'s ticks instead of the tick tables, e.g. stored history joined to
	/// live ticks not yet written, so the snapshot can follow every tick. Called off the render thread, once per symbol.
	/// </summary>
	public interface ICompareTxFoldTickSource
	{
		/// <summary>The symbol's ticks in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), oldest first.</summary>
		IReadOnlyList<Tick> ReadTicks(CompareTxFoldSymbol symbol, DateTime? historyBeforeUtc, DateTime fromUtc, DateTime toUtc);
	}
}
