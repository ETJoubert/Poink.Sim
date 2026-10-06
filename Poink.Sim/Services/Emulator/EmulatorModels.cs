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
public readonly record struct PopulationLevel(double Price, int BotCount, double Equity);

/// <summary>
/// A group of bots that start with the same capital (also what each gets on a replenish) and act alike: when a bot acts
/// it buys, sells or closes its position with these chances, and does nothing for whatever is left up to 1.
/// </summary>
public sealed record PopulationSettings(
	string Name,
	int BotCount,
	double StartingPoints,
	double BuyChance = 0.5,
	double SellChance = 0.5,
	double CloseChance = 0
)
{
	public double ActionChanceTotal => BuyChance + SellChance + CloseChance;

	public bool HasValidActionChances =>
		BuyChance is >= 0 and <= 1 && SellChance is >= 0 and <= 1 && CloseChance is >= 0 and <= 1 && ActionChanceTotal <= 1 + 1e-9;
}

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

	/// <summary>Profit/loss as a share of capital paid in, so groups of any size compare fairly.</summary>
	public double Return => Capital > 0 ? ProfitLoss / Capital : 0;
}

/// <summary>
/// One bot's account as the page shows it: capital is every point paid in; equity is its cash plus its open position at
/// the last price.
/// </summary>
public sealed record BotSummary(
	int Id,
	string Population,
	double Capital,
	double Equity,
	long Position,
	double AverageEntryPrice,
	int OpenOrders,
	double ActivityChance,
	bool IsBust
);

/// <summary>
/// The broker's fund (its balance and every point paid into it), what it has lent out, the losses it has absorbed and the
/// market's activity.
/// </summary>
public sealed record BrokerSummary(
	double FundBalance,
	double FundCapital,
	double Lent,
	double AbsorbedLosses,
	double Leverage,
	long TradeCount,
	long TradedVolume,
	long RejectedOrders,
	int OpenOrders
);

/// <summary>
/// One tick of the synthetic asset's history: the open, high, low and close of every trade in it, with
/// <see cref="Price"/> as the close.
/// </summary>
public readonly record struct EmulatorPricePoint(DateTime Timestamp, double Open, double High, double Low, double Price)
{
	public EmulatorPricePoint(DateTime timestamp, double price)
		: this(timestamp, price, price, price, price) { }
}

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
	public double ActivityChanceMin { get; init; } = 0.05;
	public double ActivityChanceMax { get; init; } = 0.15;
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
	public const double MinBrokerFund = 0;
	public const double MaxBrokerFund = 1_000_000_000_000;
	public const int MinOrderRangeTicks = 1;
	public const int MaxOrderRangeTicks = 1_000;

	/// <summary>
	/// Whether moving to <paramref name="next"/> needs a fresh market: only removing a group or lowering a group's bot
	/// count does, since bots holding positions cannot simply leave. Groups are matched by position, so adding a group or
	/// bots, renaming, and new capital or chances apply to the running market, as do speed, leverage, broker fund, order
	/// range and the action frequency range.
	/// </summary>
	public bool RequiresReopen(EmulatorSettings next) =>
		next.Populations.Count < Populations.Count
		|| Populations.Zip(next.Populations).Any(pair => pair.Second.BotCount < pair.First.BotCount)
		|| this with
		{
			SpeedMultiplier = next.SpeedMultiplier,
			Leverage = next.Leverage,
			BrokerFund = next.BrokerFund,
			OrderRangeTicks = next.OrderRangeTicks,
			ActivityChanceMin = next.ActivityChanceMin,
			ActivityChanceMax = next.ActivityChanceMax,
			Populations = next.Populations,
		} != next;

	public bool ActivityRangeDiffers(EmulatorSettings next) =>
		ActivityChanceMin != next.ActivityChanceMin || ActivityChanceMax != next.ActivityChanceMax;
}
