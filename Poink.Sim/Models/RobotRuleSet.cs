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

		/// <summary>Signal groups switched off for this rule set -- their rows are not calculated and their selections are ignored.</summary>
		public HashSet<RuleSignalGroup> DisabledGroups { get; set; } = new();
	}
}
