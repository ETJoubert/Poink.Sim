using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Helpers;

namespace Poink.Sim.Models;

/// <summary>
/// One immutable rule-engine configuration the robot evaluates: the chosen Above/In/Below keys, the XGrid Top % cutoff,
/// the In band around baseline levels (pips), and how many selected rows must match (null means all).
/// </summary>
internal sealed record RobotRules(
	HashSet<string> EnabledKeys,
	double XGridPercent,
	HashSet<RuleSignalGroup> DisabledGroups,
	double InTolerancePips,
	int? MinMatches
);

/// <summary>
/// Everything the robot needs to act on its own, pushed to the simulation engine whenever the user
/// changes any of it -- the on/off switch, how positions are sized, the open/close conditions, and the
/// window settings those conditions are evaluated over, and how often (in simulated seconds) the bots look at them.
/// After a close, a bot waits CooldownSeconds of simulated time before opening again and, with RequireRearm, also waits
/// until that side's open conditions have stopped matching once.
/// </summary>
internal sealed record RobotOptions(
	bool Enabled,
	double EvaluationIntervalSeconds,
	TradeConsoleOptions Position,
	RobotRules OpenLong,
	RobotRules OpenShort,
	RobotRules CloseLong,
	RobotRules CloseShort,
	int BarWindow,
	int BarOffset,
	int XGridPips,
	int XGridCols,
	HashSet<int> TxFoldSeconds,
	double CooldownSeconds,
	bool RequireRearm
)
{
	private IEnumerable<RobotRules> AllRules => new[] { OpenLong, OpenShort, CloseLong, CloseShort };

	public bool HasRules => AllRules.Any(rules => rules.EnabledKeys.Count > 0);

	// A group counts as used only while some rule set has it switched on and a selection in it.
	public bool UsesGroup(RuleSignalGroup group) => AllRules.Any(rules => Uses(rules, group));

	public RobotRules CloseRulesFor(bool isLong) => isLong ? CloseLong : CloseShort;

	private static bool Uses(RobotRules rules, RuleSignalGroup group) =>
		!rules.DisabledGroups.Contains(group) && rules.EnabledKeys.Any(k => RuleEngineHelper.GroupOf(k) == group);
}
