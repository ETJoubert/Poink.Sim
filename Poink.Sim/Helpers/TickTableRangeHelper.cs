using System;

namespace Poink.Sim.Helpers;

/// <summary>
/// Resolves the From/To a ticks_&lt;symbol&gt; table opens on when it is picked, so every symbol picker
/// lands on the same slice of history.
/// </summary>
public static class TickTableRangeHelper
{
	private static readonly TimeSpan DefaultSpan = TimeSpan.FromDays(1);

	/// <summary>The table's whole range when shorter than a day, otherwise its most recent day.</summary>
	public static (DateTime From, DateTime To) DefaultRange(DateTime? minTime, DateTime maxTime) =>
		minTime is { } min && maxTime - min < DefaultSpan ? (min, maxTime) : (maxTime - DefaultSpan, maxTime);

	/// <summary>True when [from, to] shares at least one instant with the table's [minTime, maxTime].</summary>
	public static bool Overlaps(DateTime from, DateTime to, DateTime? minTime, DateTime? maxTime) =>
		(minTime is null || to >= minTime.Value) && (maxTime is null || from <= maxTime.Value);
}
