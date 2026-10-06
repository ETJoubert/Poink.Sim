using System.Collections.Generic;

namespace Poink.Sim.Emulator.Bots;

/// <summary>
/// All one bot may see when it decides: public market data, its own account and its own group's buy/sell/close chances.
/// It carries nothing about other bots.
/// </summary>
public sealed record BotMarketView(
	double LastPrice,
	double? BestBid,
	double? BestAsk,
	double TickSize,
	double Cash,
	long Position,
	double Equity,
	long BuyCapacity,
	long SellCapacity,
	IReadOnlyList<long> OpenOrderIds,
	double BuyChance = 0.5,
	double SellChance = 0.5,
	double CloseChance = 0
);

/// <summary>What a bot does on its turn.</summary>
public abstract record BotDecision
{
	public static BotDecision Hold { get; } = new HoldDecision();
}

/// <summary>The bot does nothing this turn.</summary>
public sealed record HoldDecision : BotDecision;

/// <summary>The bot places a limit order at a price and volume of its choosing.</summary>
public sealed record PlaceOrderDecision(OrderSide Side, double Price, long Volume) : BotDecision;

/// <summary>The bot cancels one of its own resting orders.</summary>
public sealed record CancelOrderDecision(long OrderId) : BotDecision;

/// <summary>The bot closes its whole position at market.</summary>
public sealed record ClosePositionDecision : BotDecision;
