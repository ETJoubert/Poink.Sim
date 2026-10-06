using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>The medals awarded to the best-performing groups.</summary>
internal enum EmulatorMedal
{
	Gold,
	Silver,
	Bronze,
}

/// <summary>
/// Ranks groups by return on the capital paid in and awards gold, silver and bronze to the top three. Ties go to the
/// name that sorts first so medals do not flicker between equal groups; a lone group wins nothing.
/// </summary>
internal static class EmulatorGroupRankingHelper
{
	private static readonly EmulatorMedal[] Podium = [EmulatorMedal.Gold, EmulatorMedal.Silver, EmulatorMedal.Bronze];

	public static IReadOnlyDictionary<string, EmulatorMedal> Medals(IReadOnlyList<PopulationSummary> groups) =>
		groups.Count < 2
			? new Dictionary<string, EmulatorMedal>()
			: groups
				.OrderByDescending(group => group.Return)
				.ThenBy(group => group.Name, StringComparer.Ordinal)
				.Zip(Podium)
				.ToDictionary(pair => pair.First.Name, pair => pair.Second);
}
