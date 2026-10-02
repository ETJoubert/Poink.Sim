using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader;

namespace Poink.Sim.Bots;

/// <summary>One bot and the trading account it owns.</summary>
internal sealed class Bot
{
	public required int Id { get; init; }
	public required string Label { get; init; }
	public required ITrader Account { get; init; }
}

/// <summary>What the simulation hands the bots on every tick: the quote, the recent bars, the data range being replayed, the typical spread and the bar size.</summary>
internal readonly record struct BotTickContext(
	DateTime Timestamp,
	double Price,
	double Bid,
	double Ask,
	IReadOnlyList<HlocAvdtBar> RecentBars,
	IReadOnlyList<BarPriceHistogram> RecentHistograms,
	string Table,
	DateTime RangeStart,
	double PipSize,
	double? TypicalSpread,
	TimeSpan BarSize
);

/// <summary>A trade a bot just made on its account, for the simulation to log.</summary>
internal sealed record BotAction(Bot Bot, TradeExecution Execution, string Description);
