using System.Collections.Generic;
using Poink.Sim.Helpers;

namespace Poink.Sim.Models
{
	/// <summary>
	/// One rule-engine configuration the robot evaluates -- the Above/In/Below keys chosen in the
	/// rule panel plus the XGrid Top % cutoff those rows use. Mutable and parameterless so the panel can
	/// edit it in place and the Simulation page can persist it with its other settings.
	/// </summary>
	public sealed class RobotRuleSet
	{
		public HashSet<string> EnabledKeys { get; set; } = new();

		public double XGridPercent { get; set; } = 10;

		/// <summary>Signal groups switched off for this rule set -- their rows are not calculated and their selections ignored.</summary>
		public HashSet<RuleSignalGroup> DisabledGroups { get; set; } = new();

		/// <summary>How close, in pips, price must be to a baseline level to count as In it; Above/Below start beyond this band.</summary>
		public double InTolerancePips { get; set; } = 1;

		/// <summary>How many selected rows must match for the rule set to match; null or 0 means all of them.</summary>
		public int? MinMatches { get; set; }

		public RobotRuleSet Clone() =>
			new()
			{
				EnabledKeys = new HashSet<string>(EnabledKeys),
				XGridPercent = XGridPercent,
				DisabledGroups = new HashSet<RuleSignalGroup>(DisabledGroups),
				InTolerancePips = InTolerancePips,
				MinMatches = MinMatches,
			};
	}
}
