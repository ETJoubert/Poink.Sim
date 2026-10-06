using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The groups side by side: one column per group, headed by its colour and name, and one row per measure of its bots'
	/// combined account and trading. The three groups with the best return wear gold, silver and bronze.
	/// </summary>
	public partial class EmulatorPopulationsComponent : ComponentBase
	{
		private static readonly IReadOnlyList<SummaryRow> Rows =
		[
			new("BOTS", "Bots still trading / bots in the group -- a bot is out once its equity is gone",
				p => $"{p.ActiveBots} / {p.BotCount}", p => $"{p.ActiveBots} of {p.BotCount} bots still trading"),
			new("CAPITAL", "Points paid in, in total: starting capital plus every replenish",
				p => Compact(p.Capital), p => $"{Amount(p.Capital)} points paid in"),
			new("EQUITY", "Cash plus open positions valued at the last price, in total",
				p => Compact(p.Equity), p => $"{Amount(p.Equity)} equity"),
			new("P/L", "Equity minus capital paid in",
				p => Compact(p.ProfitLoss), p => $"{Amount(p.ProfitLoss)} profit/loss", p => SignClass(p.ProfitLoss)),
			new("RETURN", "Profit/loss as a percentage of capital paid in -- what the gold, silver and bronze medals rank by",
				p => Percent(p.Return), p => $"{Percent(p.Return)} return on capital", p => SignClass(p.Return)),
			new("CASH", "Cash in total -- negative cash is points borrowed from the broker for leveraged buys",
				p => Compact(p.Cash), p => $"{Amount(p.Cash)} cash", p => SignClass(p.Cash)),
			new("LONG", "Units held long in total",
				p => Count(p.LongUnits), p => $"{Count(p.LongUnits)} units long"),
			new("SHORT", "Units sold short in total -- borrowed from the broker",
				p => Count(p.ShortUnits), p => $"{Count(p.ShortUnits)} units short"),
			new("ORDERS", "Orders resting on the book",
				p => Count(p.OpenOrders), p => $"{Count(p.OpenOrders)} orders resting"),
			new("FILLS", "Fills received, as buyer or seller",
				p => Count(p.Fills), p => $"{Count(p.Fills)} fills"),
			new("STOP-OUTS", "Times a bot was stopped out for falling below the stop-out margin level",
				p => Count(p.StopOuts), p => $"{Count(p.StopOuts)} stop-outs"),
		];

		[Parameter, EditorRequired]
		public IReadOnlyList<PopulationSummary> Populations { get; set; } = Array.Empty<PopulationSummary>();

		/// <summary>The groups in their settings order, which fixes each group's colour.</summary>
		[Parameter]
		public IReadOnlyList<PopulationSettings> Groups { get; set; } = Array.Empty<PopulationSettings>();

		private IReadOnlyDictionary<string, EmulatorMedal> _medals = new Dictionary<string, EmulatorMedal>();

		protected override void OnParametersSet()
		{
			_medals = EmulatorGroupRankingHelper.Medals(Populations);
		}

		private static string MedalLabel(EmulatorMedal medal) =>
			medal switch
			{
				EmulatorMedal.Gold => "1st",
				EmulatorMedal.Silver => "2nd",
				_ => "3rd",
			};

		private string RankTooltip(PopulationSummary group) =>
			_medals.TryGetValue(group.Name, out var medal)
				? $"{group.Name} -- {medal}: {MedalLabel(medal)} best return of all groups at {Percent(group.Return)}"
				: $"{group.Name} -- no medal, return {Percent(group.Return)}";

		private string ColorOf(string group)
		{
			var index = Groups.Select(g => g.Name).ToList().IndexOf(group);
			return index < 0 ? "var(--color-secondary)" : EmulatorGroupColorHelper.ColorVar(index);
		}

		private static string Compact(double amount) => EmulatorChartHelper.CompactAmount(amount);

		private static string Amount(double amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

		private static string Percent(double fraction) => fraction.ToString("+0.00%;-0.00%;0.00%", CultureInfo.InvariantCulture);

		private static string Count(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

		private static string SignClass(double amount) =>
			amount > 0 ? "emu-pop-positive"
			: amount < 0 ? "emu-pop-negative"
			: string.Empty;

		private sealed record SummaryRow(
			string Label,
			string Tooltip,
			Func<PopulationSummary, string> Value,
			Func<PopulationSummary, string> CellTooltip,
			Func<PopulationSummary, string>? CssClass = null
		);
	}
}
