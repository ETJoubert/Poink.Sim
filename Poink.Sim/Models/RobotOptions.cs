using System.Collections.Generic;
using Poink.Sim.Components.Shared.TradeConsole;

namespace Poink.Sim.Models;

/// <summary>One immutable rule-engine configuration the robot evaluates: the chosen Above/In/Below keys and the XGrid Top % cutoff.</summary>
internal sealed record RobotRules(HashSet<string> EnabledKeys, double XGridPercent);

/// <summary>
/// Everything the robot needs to act on its own, pushed to the simulation engine whenever the user
/// changes any of it -- the on/off switch, how positions are sized, the open/close conditions, and the
/// window settings those conditions are evaluated over, and how often (in simulated seconds) the bots look at them.
/// </summary>
internal sealed record RobotOptions(
	bool Enabled,
	double EvaluationIntervalSeconds,
	TradeConsoleOptions Position,
	RobotRules OpenLong,
	RobotRules OpenShort,
	RobotRules Close,
	int BarWindow,
	int BarOffset,
	int XGridPips,
	int XGridCols,
	HashSet<int> TxFoldSeconds
)
{
	public bool HasRules => OpenLong.EnabledKeys.Count > 0 || OpenShort.EnabledKeys.Count > 0 || Close.EnabledKeys.Count > 0;

	public bool UsesTxFold =>
		Helpers.RuleEngineHelper.UsesTxFold(OpenLong.EnabledKeys)
		|| Helpers.RuleEngineHelper.UsesTxFold(OpenShort.EnabledKeys)
		|| Helpers.RuleEngineHelper.UsesTxFold(Close.EnabledKeys);
}
