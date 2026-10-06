using System;

namespace Poink.Sim.Emulator.Bots;

/// <summary>
/// Zero-intelligence trader: sometimes cancels one of its resting orders at random, otherwise picks a random side,
/// a random level within <see cref="EmulatorSettings.OrderRangeTicks"/> of the last price and a random volume
/// its margin allows.
/// </summary>
public sealed class RandomBotStrategy : IEmulatorBotStrategy
{
	private readonly Random _random;

	public RandomBotStrategy()
		: this(new Random()) { }

	internal RandomBotStrategy(Random random)
	{
		_random = random;
	}

	public BotDecision Decide(BotMarketView view, EmulatorSettings settings)
	{
		if (view.OpenOrderIds.Count > 0 && _random.NextDouble() < settings.CancelChance)
		{
			return new CancelOrderDecision(view.OpenOrderIds[_random.Next(view.OpenOrderIds.Count)]);
		}

		var side = _random.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell;
		var capacity = side == OrderSide.Buy ? view.BuyCapacity : view.SellCapacity;
		var maxVolume = Math.Min(capacity, Math.Max(1, (long)Math.Floor(capacity * settings.MaxOrderFraction)));
		if (maxVolume < 1)
		{
			return BotDecision.Hold;
		}

		var offsetTicks = _random.Next(-settings.OrderRangeTicks, settings.OrderRangeTicks + 1);
		var price = Math.Max(view.TickSize, view.LastPrice + offsetTicks * view.TickSize);
		return new PlaceOrderDecision(side, price, _random.NextInt64(1, maxVolume + 1));
	}
}
