using System;
using System.Collections.Generic;

namespace Poink.Sim.Emulator;

/// <summary>One price level of the order book: the volume resting at that price.</summary>
public readonly record struct OrderBookLevel(double Price, double Volume);

/// <summary>Resting buy (bid) and sell (ask) levels, each ordered best price first.</summary>
public sealed record OrderBook(IReadOnlyList<OrderBookLevel> Bids, IReadOnlyList<OrderBookLevel> Asks)
{
	public static OrderBook Empty { get; } = new(Array.Empty<OrderBookLevel>(), Array.Empty<OrderBookLevel>());

	public double? BestBid => Bids.Count > 0 ? Bids[0].Price : null;

	public double? BestAsk => Asks.Count > 0 ? Asks[0].Price : null;

	public double? Spread => BestAsk - BestBid;
}

/// <summary>The bots positioned at one price level and the balance they hold between them.</summary>
public readonly record struct PopulationLevel(double Price, int BotCount, double Balance);

/// <summary>The synthetic asset's price at one moment of its history.</summary>
public readonly record struct EmulatorPricePoint(DateTime Timestamp, double Price);

/// <summary>Everything the Emulator page shows at one step: the asset price, its order book, the bot population per level and its price history.</summary>
public sealed record EmulatorSnapshot(
	string Symbol,
	double Price,
	OrderBook Book,
	IReadOnlyList<PopulationLevel> Population,
	IReadOnlyList<EmulatorPricePoint> History,
	long Step
);

/// <summary>Shape of the emulated market: the synthetic asset, its price grid, book depth and how fast it steps.</summary>
public sealed record EmulatorSettings
{
	public string Symbol { get; init; } = "SYN";
	public double StartPrice { get; init; } = 100;
	public double TickSize { get; init; } = 0.01;
	public int SpreadTicks { get; init; } = 2;
	public int BookDepth { get; init; } = 15;
	public double VolatilityTicks { get; init; } = 4;
	public double VolumePerBot { get; init; } = 10;
	public double MeanBotBalance { get; init; } = 5_000;
	public int HistoryCapacity { get; init; } = 300;
	public int WarmupSteps { get; init; } = 120;
	public TimeSpan StepInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}
