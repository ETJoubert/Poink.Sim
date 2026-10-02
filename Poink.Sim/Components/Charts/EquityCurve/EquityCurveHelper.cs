using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts;

internal static class EquityCurveHelper
{
	/// <summary>
	/// Pairs an equity series with its own timestamps and the trades taken over that span, and
	/// emits one sample per trade: the equity at the trade's last covered point (its outcome) plus
	/// the best/worst equity seen between the trade's entry and that point. Trades with no covered
	/// points yield no sample. Assumes at most one trade open at a time, matching this app's
	/// single-position trader.
	/// </summary>
	public static List<EquityCurveSample> Build(
		IReadOnlyList<DateTime> timestamps,
		IReadOnlyList<double> equity,
		IReadOnlyList<Trade> trades
	)
	{
		int count = Math.Min(timestamps.Count, equity.Count);
		var samples = new List<EquityCurveSample>();
		if (count == 0)
			return samples;

		var orderedTrades = trades.OrderBy(t => t.EntryTime).ToList();
		int tradeIndex = 0;
		int? openTradeIndex = null;
		DateTime lastTime = default;
		double lastEquity = 0;
		double runningMax = 0;
		double runningMin = 0;

		void CloseOpenTrade()
		{
			if (openTradeIndex is int n)
				samples.Add(new EquityCurveSample(lastTime, lastEquity, runningMax, runningMin, n + 1));
			openTradeIndex = null;
		}

		for (int i = 0; i < count; i++)
		{
			var time = timestamps[i];
			var eq = equity[i];

			while (tradeIndex < orderedTrades.Count && orderedTrades[tradeIndex].ExitTime < time)
				tradeIndex++;

			bool covered =
				tradeIndex < orderedTrades.Count
				&& orderedTrades[tradeIndex].EntryTime <= time
				&& time <= orderedTrades[tradeIndex].ExitTime;

			if (!covered)
			{
				CloseOpenTrade();
				continue;
			}

			if (openTradeIndex != tradeIndex)
			{
				CloseOpenTrade();
				openTradeIndex = tradeIndex;
				runningMax = eq;
				runningMin = eq;
			}
			else
			{
				runningMax = Math.Max(runningMax, eq);
				runningMin = Math.Min(runningMin, eq);
			}

			lastTime = time;
			lastEquity = eq;
		}

		CloseOpenTrade();
		return samples;
	}
}
