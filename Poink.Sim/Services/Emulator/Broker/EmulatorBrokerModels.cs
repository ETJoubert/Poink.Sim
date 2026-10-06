using System;

namespace Poink.Sim.Emulator.Broker;

/// <summary>
/// One bot's account at the broker. Negative cash is points the broker lent for a leveraged buy; a negative position is
/// units it lent for a short sale. Capital is every point paid in (starting capital plus replenishments); balance is
/// capital plus realised P/L; equity adds the open position's unrealised P/L at the given price.
/// </summary>
internal sealed class EmulatorBotAccount(int id, string population, double startingCapital)
{
	public int Id { get; } = id;
	public string Population { get; } = population;
	public double Capital { get; private set; } = startingCapital;
	public double Cash { get; private set; } = startingCapital;
	public long Position { get; private set; }
	public double AverageEntryPrice { get; private set; }
	public bool IsBust { get; private set; }
	public int Fills { get; private set; }
	public int StopOuts { get; set; }

	public double Balance => Cash + Position * AverageEntryPrice;

	public double Equity(double price) => Cash + Position * price;

	public double Borrowed(double price) => Math.Max(0, -Cash) + Math.Max(0, -Position) * price;

	public void ApplyFill(OrderSide side, long volume, double price)
	{
		var signedVolume = side == OrderSide.Buy ? volume : -volume;
		var newPosition = Position + signedVolume;
		AverageEntryPrice = newPosition == 0 ? 0
			: Position == 0 || Math.Sign(Position) == Math.Sign(signedVolume)
				? (AverageEntryPrice * Math.Abs(Position) + price * volume) / Math.Abs(newPosition)
			: Math.Sign(newPosition) != Math.Sign(Position) ? price
			: AverageEntryPrice;

		Cash -= signedVolume * price;
		Position = newPosition;
		Fills++;
	}

	/// <summary>Pays points in on top of what the bot has; a bust bot is back in the market.</summary>
	public void Deposit(double amount)
	{
		Cash += amount;
		Capital += amount;
		IsBust = false;
	}

	public void GoBust()
	{
		Cash = 0;
		IsBust = true;
	}
}

/// <summary>How the broker answered an order.</summary>
internal enum OrderOutcome
{
	Accepted,
	RejectedInvalid,
	RejectedMargin,
	RejectedFund,
}
