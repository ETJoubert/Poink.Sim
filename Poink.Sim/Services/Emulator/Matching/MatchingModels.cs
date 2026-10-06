namespace Poink.Sim.Emulator.Matching;

/// <summary>An order waiting on the book, with the volume still unfilled.</summary>
internal sealed class RestingOrder
{
	public required long Id { get; init; }
	public required int BotId { get; init; }
	public required OrderSide Side { get; init; }
	public required long PriceTicks { get; init; }
	public required long Remaining { get; set; }
}

/// <summary>A new order for the engine: who sends it, which side, its limit price in ticks and its volume.</summary>
internal readonly record struct OrderSubmission(int BotId, OrderSide Side, long PriceTicks, long Volume);

/// <summary>A fill between a buyer and a seller, at the resting order's price.</summary>
internal readonly record struct EmulatorTrade(long PriceTicks, long Volume, int BuyBotId, int SellBotId);
