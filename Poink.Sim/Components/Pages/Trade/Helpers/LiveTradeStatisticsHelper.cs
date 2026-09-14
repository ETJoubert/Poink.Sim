using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Helpers;

/// <summary>
/// Trade-outcome statistics computed from closed-deal history alone (<see cref="CTraderDealInfo"/>), for the
/// Financials panel's Statistics tab. Mirrors <c>TradeStatistics</c>' groups where cTrader's deal history
/// carries enough to compute them; metrics needing an entry time, a close reason, or a continuous equity
/// curve are reported as unavailable instead of guessed at, since a closing deal alone doesn't carry them.
/// </summary>
public static class LiveTradeStatisticsHelper
{
	private static string Money(double value) => value.ToString("N2");

	private static StatEmphasis EmphasisFor(double value) =>
		value switch
		{
			> 0 => StatEmphasis.Positive,
			< 0 => StatEmphasis.Negative,
			_ => StatEmphasis.None,
		};

	public static IReadOnlyList<StatGroup> From(IReadOnlyList<CTraderDealInfo> deals)
	{
		var wins = deals.Where(d => d.RealizedPnl is > 0).ToList();
		var losses = deals.Where(d => d.RealizedPnl is < 0).ToList();
		var longsClosed = deals.Count(d => d.IsLong);
		var shortsClosed = deals.Count(d => !d.IsLong);
		var totalCommission = deals.Sum(d => d.Commission ?? 0);
		var totalSwap = deals.Sum(d => d.Swap ?? 0);

		int longestWinStreak = 0,
			longestLossStreak = 0,
			currentWinStreak = 0,
			currentLossStreak = 0;
		foreach (var deal in deals.OrderBy(d => d.ExecutionTimeUtc))
		{
			var pnl = deal.RealizedPnl ?? 0;
			if (pnl > 0)
			{
				currentWinStreak++;
				currentLossStreak = 0;
			}
			else if (pnl < 0)
			{
				currentLossStreak++;
				currentWinStreak = 0;
			}
			else
			{
				currentWinStreak = 0;
				currentLossStreak = 0;
			}
			longestWinStreak = Math.Max(longestWinStreak, currentWinStreak);
			longestLossStreak = Math.Max(longestLossStreak, currentLossStreak);
		}

		return new[]
		{
			new StatGroup(
				"Trade Outcomes",
				new[]
				{
					new StatItem("Won trades", wins.Count.ToString("N0"), StatEmphasis.Positive),
					new StatItem("Lost trades", losses.Count.ToString("N0"), StatEmphasis.Negative),
					new StatItem(
						"Biggest win",
						Money(wins.Count > 0 ? wins.Max(d => d.RealizedPnl!.Value) : 0),
						StatEmphasis.Positive
					),
					new StatItem(
						"Biggest loss",
						Money(losses.Count > 0 ? losses.Min(d => d.RealizedPnl!.Value) : 0),
						StatEmphasis.Negative
					),
					new StatItem(
						"Average win",
						Money(wins.Count > 0 ? wins.Average(d => d.RealizedPnl!.Value) : 0),
						StatEmphasis.Positive
					),
					new StatItem(
						"Average loss",
						Money(losses.Count > 0 ? losses.Average(d => d.RealizedPnl!.Value) : 0),
						StatEmphasis.Negative
					),
				}
			),
			new StatGroup(
				"Activity",
				new[]
				{
					new StatItem("Longs closed", longsClosed.ToString("N0")),
					new StatItem("Shorts closed", shortsClosed.ToString("N0")),
				}
			),
			new StatGroup(
				"Costs",
				new[]
				{
					new StatItem("Total commission", Money(totalCommission), StatEmphasis.Negative),
					new StatItem("Total swap", Money(totalSwap), EmphasisFor(totalSwap)),
				}
			),
			new StatGroup(
				"Streaks",
				new[]
				{
					new StatItem("Longest winning streak", longestWinStreak.ToString("N0"), StatEmphasis.Positive),
					new StatItem("Longest losing streak", longestLossStreak.ToString("N0"), StatEmphasis.Negative),
				}
			),
			new StatGroup(
				"Not Available From Deal History",
				new[]
				{
					new StatItem("Time in market", "Needs entry time -- cTrader deal history only carries the close"),
					new StatItem("Stop-loss / take-profit hits", "Needs a close reason -- not reported by cTrader"),
					new StatItem("Max equity / max drawdown", "Needs a continuous equity curve, not just closed deals"),
				}
			),
		};
	}
}
