using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models.TrainingModels
{
	internal sealed record TrainingStrategyInfo(string Name, string Summary, string Reward, string Updates, string Memory, string Caveat);

	/// <summary>Names and plain-language descriptions of the training strategies the engine supports.</summary>
	internal static class TrainingStrategyCatalog
	{
		public const string ReinforceName = "Policy Gradient (REINFORCE)";
		public const string TradeOutcomeName = "Policy Gradient (Trade Outcome)";

		private const string LegacyPerDecisionName = "Policy Gradient (Per-Decision)";

		public static IReadOnlyList<TrainingStrategyInfo> All { get; } =
		[
			new(
				ReinforceName,
				"Learns from whole episodes: every decision is recorded, then one averaged update is applied when the episode ends.",
				"Discounted equity change after each decision, normalised by the episode's own mean and spread.",
				"Once per episode.",
				"Keeps every decision's activations until the episode ends.",
				"The last decision of an episode has no following reward and is dropped."
			),
			new(
				TradeOutcomeName,
				"Learns to choose a direction: an entry is rewarded with the realized value of the trade it opened, however that trade ended.",
				"Realized profit or loss of the trade after commission, credited when it closes. Holding while flat counts as 0. Judged against a running baseline of past trade results.",
				"Immediately, each time a trade closes and each time the network holds while flat.",
				"Constant -- only the entry awaiting its trade is kept.",
				"A trade still open when the episode ends is not credited. Choices made while a position is open are not learned from."
			),
		];

		public static IReadOnlyList<string> Names { get; } = All.Select(info => info.Name).ToList();

		public static TrainingStrategyInfo? Find(string name) => All.FirstOrDefault(info => info.Name == Normalize(name));

		public static string Normalize(string name) => name == LegacyPerDecisionName ? TradeOutcomeName : name;
	}
}
