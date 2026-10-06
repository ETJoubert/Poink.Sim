using System;

namespace Poink.Sim.Emulator.Broker;

/// <summary>
/// One bot's account at the broker. Negative cash is points the broker lent for a leveraged buy; a negative position is
/// units it lent for a short sale.
/// </summary>
internal sealed class EmulatorBotAccount
{
	public required int Id { get; init; }
	public required string Population { get; init; }
	public required double StartingPoints { get; init; }
	public required double Cash { get; set; }
	public long Position { get; set; }
	public bool IsBust { get; set; }
	public int Fills { get; set; }
	public int StopOuts { get; set; }

	public double Equity(double price) => Cash + Position * price;

	public double Borrowed(double price) => Math.Max(0, -Cash) + Math.Max(0, -Position) * price;
}

/// <summary>How the broker answered an order.</summary>
internal enum OrderOutcome
{
	Accepted,
	RejectedInvalid,
	RejectedMargin,
	RejectedFund,
}
