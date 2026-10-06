using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Emulator;

/// <summary>Which side of the book an order sits on: buying (bid) or selling (ask).</summary>
public enum OrderSide
{
	Buy,
	Sell,
}

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

/// <summary>The bots with orders resting at one price level and the equity they hold between them.</summary>
public readonly record struct PopulationLevel(double Price, int BotCount, double Balance);

/// <summary>A group of bots that start with the same capital, which is also what each gets on a replenish.</summary>
public sealed record PopulationSettings(string Name, int BotCount, double StartingPoints);

/// <summary>Where one population stands now: its bots, their combined account and their trading so far.</summary>
public sealed record PopulationSummary(
	string Name,
	int BotCount,
	int ActiveBots,
	double Capital,
	double Equity,
	double Cash,
	long LongUnits,
	long ShortUnits,
	int OpenOrders,
	int Fills,
	int StopOuts
)
{
	public double ProfitLoss => Equity - Capital;
}

/// <summary>
/// One bot's account as the page shows it: capital is every point paid in, balance adds realised P/L, equity adds the
/// open position.
/// </summary>
public sealed record BotSummary(
	int Id,
	string Population,
	double Capital,
	double Balance,
	double Equity,
	long Position,
	double AverageEntryPrice,
	int OpenOrders,
	bool IsBust
);

/// <summary>The broker's fund, what it has lent out, the losses it has absorbed and the market's activity.</summary>
public sealed record BrokerSummary(
	double FundBalance,
	double Lent,
	double AbsorbedLosses,
	double Leverage,
	long TradeCount,
	long TradedVolume,
	long RejectedOrders,
	int OpenOrders
);

/// <summary>The synthetic asset's price at one moment of its history.</summary>
public readonly record struct EmulatorPricePoint(DateTime Timestamp, double Price);

/// <summary>Everything the Emulator page shows at one step: the asset price, its order book, the bots and the broker.</summary>
public sealed record EmulatorSnapshot(
	string Symbol,
	double Price,
	OrderBook Book,
	IReadOnlyList<PopulationLevel> Population,
	IReadOnlyList<PopulationSummary> Populations,
	IReadOnlyList<BotSummary> Bots,
	BrokerSummary Broker,
	IReadOnlyList<EmulatorPricePoint> History,
	long Step
);

/// <summary>Shape of the emulated market: the asset and its price grid, the bots and how they act, and the broker's terms.</summary>
public sealed record EmulatorSettings
{
	public string Symbol { get; init; } = "SYN";
	public double StartPrice { get; init; } = 100;
	public double TickSize { get; init; } = 0.01;
	public int BookDepth { get; init; } = 25;

	public IReadOnlyList<PopulationSettings> Populations { get; init; } = [new PopulationSettings("Random", 200, 10_000)];
	public double ActivityChance { get; init; } = 0.1;
	public double CancelChance { get; init; } = 0.2;
	public int OrderRangeTicks { get; init; } = 20;
	public double MaxOrderFraction { get; init; } = 0.1;

	public double Leverage { get; init; } = 2;
	public double StopOutLevelPercent { get; init; } = 50;
	public double BrokerFund { get; init; } = 1_000_000;

	public int HistoryCapacity { get; init; } = 600;
	public int WarmupSteps { get; init; } = 120;
	public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(50);
	public int SpeedMultiplier { get; init; } = 1;

	public static IReadOnlyList<int> SpeedMultipliers { get; } = [1, 10, 100, 1_000];
	public const double MinLeverage = 1;
	public const double MaxLeverage = 100;

	/// <summary>
	/// Whether moving to <paramref name="next"/> needs a fresh market. Speed and leverage apply to the running market;
	/// anything else, such as the populations, does not.
	/// </summary>
	public bool RequiresReopen(EmulatorSettings next) =>
		!Populations.SequenceEqual(next.Populations)
		|| this with { SpeedMultiplier = next.SpeedMultiplier, Leverage = next.Leverage, Populations = next.Populations } != next;
}
