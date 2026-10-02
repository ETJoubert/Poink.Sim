using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Bots;

/// <summary>
/// Owns the bots and their trading accounts and decides, on each simulation tick, when they trade.
/// The simulation feeds it market data; it applies the resulting orders to the bots' accounts.
/// </summary>
internal interface IBotService
{
	IReadOnlyList<Bot> Bots { get; }

	/// <summary>Replaces every bot with a fresh one holding a new account set up as the options describe.</summary>
	IReadOnlyList<Bot> CreateBots(BotAccountOptions account, IReadOnlyList<string>? riskPolicyNames, RiskPolicyToggles? riskToggles);

	/// <summary>Updates the options every bot trades by; applied from the next tick.</summary>
	void SetOptions(RobotOptions options);

	IReadOnlyList<BotAction> OnTick(BotTickContext tick);
}
