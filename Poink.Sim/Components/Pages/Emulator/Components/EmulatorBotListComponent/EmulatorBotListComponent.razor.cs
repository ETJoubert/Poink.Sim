using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// Every bot's account: its position, balance (realised) and equity (with the open position), bust bots dimmed.
	/// </summary>
	public partial class EmulatorBotListComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<BotSummary> Bots { get; set; } = Array.Empty<BotSummary>();

		private static string BotTooltip(BotSummary bot) =>
			string.Create(
				CultureInfo.InvariantCulture,
				$"Bot #{bot.Id} ({bot.Population}){(bot.IsBust ? " -- bust, no longer trading" : "")}: position {bot.Position:N0}"
					+ $"{(bot.Position != 0 ? $" at an average {bot.AverageEntryPrice:N4}" : "")}, balance {bot.Balance:N2}, equity {bot.Equity:N2}, "
					+ $"{bot.OpenOrders:N0} orders resting"
			);

		private static string SignClass(double amount) =>
			amount > 0 ? "emu-bots-positive"
			: amount < 0 ? "emu-bots-negative"
			: string.Empty;

		private static string EquityClass(BotSummary bot) => SignClass(bot.Equity - bot.StartingPoints);
	}
}
