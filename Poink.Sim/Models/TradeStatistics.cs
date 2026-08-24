using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models;

internal sealed class TradeStatistics
{
	public int WinCount { get; }
	public int LossCount { get; }
	public double BiggestWin { get; }
	public double BiggestLoss { get; }
	public double AverageWin { get; }
	public double AverageLoss { get; }
	public double TotalCommission { get; }
	public int BuyCount { get; }
	public int SellCount { get; }
	public TimeSpan TimeInMarket { get; }
	public TimeSpan TimeInLong { get; }
	public TimeSpan TimeInShort { get; }
	public TimeSpan AverageTimeInLong { get; }
	public TimeSpan AverageTimeInShort { get; }
	public double MaxEquity { get; }
	public double MaxDrawdown { get; }
	public int LongestWinStreak { get; }
	public int LongestLossStreak { get; }
	public int StopLossHitCount { get; }
	public double StopLossPnl { get; }
	public int TakeProfitHitCount { get; }
	public double TakeProfitPnl { get; }

	private TradeStatistics(
		int winCount,
		int lossCount,
		double biggestWin,
		double biggestLoss,
		double averageWin,
		double averageLoss,
		double totalCommission,
		int buyCount,
		int sellCount,
		TimeSpan timeInMarket,
		TimeSpan timeInLong,
		TimeSpan timeInShort,
		TimeSpan averageTimeInLong,
		TimeSpan averageTimeInShort,
		double maxEquity,
		double maxDrawdown,
		int longestWinStreak,
		int longestLossStreak,
		int stopLossHitCount,
		double stopLossPnl,
		int takeProfitHitCount,
		double takeProfitPnl
	)
	{
		WinCount = winCount;
		LossCount = lossCount;
		BiggestWin = biggestWin;
		BiggestLoss = biggestLoss;
		AverageWin = averageWin;
		AverageLoss = averageLoss;
		TotalCommission = totalCommission;
		BuyCount = buyCount;
		SellCount = sellCount;
		TimeInMarket = timeInMarket;
		TimeInLong = timeInLong;
		TimeInShort = timeInShort;
		AverageTimeInLong = averageTimeInLong;
		AverageTimeInShort = averageTimeInShort;
		MaxEquity = maxEquity;
		MaxDrawdown = maxDrawdown;
		LongestWinStreak = longestWinStreak;
		LongestLossStreak = longestLossStreak;
		StopLossHitCount = stopLossHitCount;
		StopLossPnl = stopLossPnl;
		TakeProfitHitCount = takeProfitHitCount;
		TakeProfitPnl = takeProfitPnl;
	}

	public static TradeStatistics From(IReadOnlyList<Trade> trades, double maxEquity, double maxDrawdown)
	{
		var wins = trades.Where(t => t.RealizedPnl > 0).ToList();
		var losses = trades.Where(t => t.RealizedPnl < 0).ToList();
		var longs = trades.Where(t => t.Direction == PositionDirection.Long).ToList();
		var shorts = trades.Where(t => t.Direction == PositionDirection.Short).ToList();
		var stopLossHits = trades.Where(t => t.CloseReason == TradeCloseReason.StopLoss).ToList();
		var takeProfitHits = trades.Where(t => t.CloseReason == TradeCloseReason.TakeProfit).ToList();

		int longestWin = 0,
			longestLoss = 0,
			curWin = 0,
			curLoss = 0;
		foreach (var t in trades)
		{
			if (t.RealizedPnl > 0)
			{
				curWin++;
				curLoss = 0;
			}
			else if (t.RealizedPnl < 0)
			{
				curLoss++;
				curWin = 0;
			}
			else
			{
				curWin = 0;
				curLoss = 0;
			}
			longestWin = Math.Max(longestWin, curWin);
			longestLoss = Math.Max(longestLoss, curLoss);
		}

		static TimeSpan Duration(Trade t) => t.ExitTime - t.EntryTime;
		static TimeSpan Sum(IEnumerable<Trade> ts) => ts.Aggregate(TimeSpan.Zero, (acc, t) => acc + Duration(t));
		static TimeSpan Average(List<Trade> ts) =>
			ts.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks((long)ts.Average(t => Duration(t).Ticks));

		return new TradeStatistics(
			wins.Count,
			losses.Count,
			wins.Count > 0 ? wins.Max(t => t.RealizedPnl) : 0,
			losses.Count > 0 ? losses.Min(t => t.RealizedPnl) : 0,
			wins.Count > 0 ? wins.Average(t => t.RealizedPnl) : 0,
			losses.Count > 0 ? losses.Average(t => t.RealizedPnl) : 0,
			trades.Sum(t => t.Commission),
			longs.Count,
			shorts.Count,
			Sum(trades),
			Sum(longs),
			Sum(shorts),
			Average(longs),
			Average(shorts),
			maxEquity,
			maxDrawdown,
			longestWin,
			longestLoss,
			stopLossHits.Count,
			stopLossHits.Sum(t => t.RealizedPnl),
			takeProfitHits.Count,
			takeProfitHits.Sum(t => t.RealizedPnl)
		);
	}

	private static string Money(double v) => v.ToString("N2");

	private static string TimeSpanStr(TimeSpan ts) => ts.ToString(@"d\.hh\:mm\:ss");

	private static StatEmphasis EmphasisFor(double v) =>
		v switch
		{
			> 0 => StatEmphasis.Positive,
			< 0 => StatEmphasis.Negative,
			_ => StatEmphasis.None,
		};

	public IReadOnlyList<StatGroup> Groups =>
		new[]
		{
			new StatGroup(
				"Trade Outcomes",
				new[]
				{
					new StatItem("Won trades", WinCount.ToString("N0"), StatEmphasis.Positive),
					new StatItem("Lost trades", LossCount.ToString("N0"), StatEmphasis.Negative),
					new StatItem("Biggest win", Money(BiggestWin), StatEmphasis.Positive),
					new StatItem("Biggest loss", Money(BiggestLoss), StatEmphasis.Negative),
					new StatItem("Average win", Money(AverageWin), StatEmphasis.Positive),
					new StatItem("Average loss", Money(AverageLoss), StatEmphasis.Negative),
				}
			),
			new StatGroup(
				"Activity",
				new[]
				{
					new StatItem("Buys (long entries)", BuyCount.ToString("N0")),
					new StatItem("Sells (short entries)", SellCount.ToString("N0")),
				}
			),
			new StatGroup(
				"Stop-Loss / Take-Profit",
				new[]
				{
					new StatItem("Stop-loss hits", StopLossHitCount.ToString("N0")),
					new StatItem("Stop-loss equity", Money(StopLossPnl), EmphasisFor(StopLossPnl)),
					new StatItem("Take-profit hits", TakeProfitHitCount.ToString("N0")),
					new StatItem("Take-profit equity", Money(TakeProfitPnl), EmphasisFor(TakeProfitPnl)),
				}
			),
			new StatGroup(
				"Costs",
				new[] { new StatItem("Total commission", Money(TotalCommission), StatEmphasis.Negative) }
			),
			new StatGroup(
				"Time in Market",
				new[]
				{
					new StatItem("Time in market", TimeSpanStr(TimeInMarket)),
					new StatItem("Time in long", TimeSpanStr(TimeInLong)),
					new StatItem("Time in short", TimeSpanStr(TimeInShort)),
					new StatItem("Average time in long", TimeSpanStr(AverageTimeInLong)),
					new StatItem("Average time in short", TimeSpanStr(AverageTimeInShort)),
				}
			),
			new StatGroup(
				"Equity",
				new[]
				{
					new StatItem("Max equity", Money(MaxEquity)),
					new StatItem("Max drawdown", Money(MaxDrawdown), StatEmphasis.Negative),
				}
			),
			new StatGroup(
				"Streaks",
				new[]
				{
					new StatItem("Longest winning streak", LongestWinStreak.ToString("N0"), StatEmphasis.Positive),
					new StatItem("Longest losing streak", LongestLossStreak.ToString("N0"), StatEmphasis.Negative),
				}
			),
		};
}
