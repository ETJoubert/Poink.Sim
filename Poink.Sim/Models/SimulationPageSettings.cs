using System;
using System.Collections.Generic;
using Poink.Sim.Components.Shared.TradeConsole;

namespace Poink.Sim.Models
{
	/// <summary>
	/// Snapshot of the Simulation page's setup form (Data Range/Trader Account/Risk
	/// Policies), persisted to browser localStorage so it survives navigating away and back --
	/// the page's own fields are reset to their defaults every time Blazor recreates the component.
	/// </summary>
	internal sealed record SimulationPageSettings(
		string Table,
		DateTime From,
		DateTime To,
		bool Aggregate,
		int BarSizeSeconds,
		double StartingCash,
		int TraderCount,
		double Leverage,
		double CommissionRatePercent,
		double StopOutLevelPercent,
		TradeConsoleOptions? RobotPositionOptions,
		RobotRuleSet? OpenLongRules,
		RobotRuleSet? OpenShortRules,
		RobotRuleSet? CloseRules,
		bool RobotEnabled,
		TradeConsoleOptions? TradeOptions,
		RobotSubTab RobotSubTab,
		RobotOpenSide RobotOpenSide,
		double? RobotEvaluationSeconds
	);
}
