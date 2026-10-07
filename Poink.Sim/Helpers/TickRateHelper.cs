using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Helpers;

/// <summary>Measures recent tick activity from a raw tick list, independent of any bar size.</summary>
internal static class TickRateHelper
{
	public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Rates over the <paramref name="window"/> ending at the newest of <paramref name="ticks"/> (oldest first),
	/// always divided by the full window. A tick before the window, when present, sets the first in-window tick's direction.
	/// </summary>
	public static TickRate? Measure(IReadOnlyList<Tick> ticks, TimeSpan window)
	{
		if (ticks.Count == 0 || window <= TimeSpan.Zero)
			return null;

		var asOf = ticks[^1].Timestamp;
		var from = asOf - window;
		int count = 0,
			up = 0,
			down = 0;
		for (var i = ticks.Count - 1; i >= 0 && ticks[i].Timestamp > from; i--)
		{
			count++;
			if (i == 0)
				break;
			double mid = Mid(ticks[i]);
			double previous = Mid(ticks[i - 1]);
			if (mid > previous)
				up++;
			else if (mid < previous)
				down++;
		}

		double seconds = window.TotalSeconds;
		return new TickRate(count / seconds, up / seconds, down / seconds, window, asOf);
	}

	private static double Mid(Tick tick) => (tick.Bid + tick.Ask) / 2.0;
}
