using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts;

internal static class EquityCurveHelper
{
	/// <summary>
	/// Pairs an equity series with its own timestamps and the trades taken over that span, so
	/// each point falling inside a trade's [EntryTime, ExitTime] carries the running best/worst
	/// equity seen since that trade's entry -- reset the moment a trade starts, null everywhere
	/// else since there's no meaningful trade extreme to show outside one. Assumes at most one
	/// trade open at a time, matching this app's single-position trader.
	/// </summary>
	public static List<EquityCurveSample> Build(
		IReadOnlyList<DateTime> timestamps,
		IReadOnlyList<double> equity,
		IReadOnlyList<Trade> trades
	)
	{
		int count = Math.Min(timestamps.Count, equity.Count);
		var samples = new List<EquityCurveSample>(count);
		if (count == 0)
			return samples;

		var orderedTrades = trades.OrderBy(t => t.EntryTime).ToList();
		int tradeIndex = 0;
		Trade? activeTrade = null;
		double runningMax = 0;
		double runningMin = 0;

		for (int i = 0; i < count; i++)
		{
			var time = timestamps[i];
			var eq = equity[i];

			while (tradeIndex < orderedTrades.Count && orderedTrades[tradeIndex].ExitTime < time)
				tradeIndex++;

			var covering =
				tradeIndex < orderedTrades.Count
				&& orderedTrades[tradeIndex].EntryTime <= time
				&& time <= orderedTrades[tradeIndex].ExitTime
					? orderedTrades[tradeIndex]
					: null;

			if (!ReferenceEquals(covering, activeTrade))
			{
				activeTrade = covering;
				runningMax = eq;
				runningMin = eq;
			}
			else if (covering is not null)
			{
				runningMax = Math.Max(runningMax, eq);
				runningMin = Math.Min(runningMin, eq);
			}

			int? tradeNumber = covering is null ? null : tradeIndex + 1;
			samples.Add(
				new EquityCurveSample(
					time,
					eq,
					covering is null ? null : runningMax,
					covering is null ? null : runningMin,
					tradeNumber
				)
			);
		}

		return samples;
	}
}
