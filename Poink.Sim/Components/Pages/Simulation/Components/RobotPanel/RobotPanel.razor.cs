using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Shared.TradeConsole;
using Poink.Sim.Helpers;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation page's Robot tab: Positioning, Open and Close sub-tabs, whose selection the host keeps. Positioning reuses
	/// <see cref="TradeConsole"/> in settings mode to capture the robot's position options; Open (split
	/// into Long and Short) and Close each host their own rule-engine configuration as the conditions to
	/// open and close.
	/// </summary>
	public partial class RobotPanel : ComponentBase
	{
		[Parameter]
		public RobotSubTab SubTab { get; set; }

		[Parameter]
		public EventCallback<RobotSubTab> OnSubTabChanged { get; set; }

		[Parameter]
		public RobotOpenSide OpenSide { get; set; }

		[Parameter]
		public EventCallback<RobotOpenSide> OnOpenSideChanged { get; set; }

		/// <summary>How often, in simulated seconds, the robot checks its conditions; 0 checks on every tick.</summary>
		[Parameter]
		public double EvaluationSeconds { get; set; }

		[Parameter]
		public EventCallback<double> OnEvaluationSecondsChanged { get; set; }

		/// <summary>Whether the robot is allowed to trade when its conditions match.</summary>
		[Parameter]
		public bool Enabled { get; set; }

		[Parameter]
		public EventCallback<bool> OnEnabledChanged { get; set; }

		[Parameter]
		public TradeConsoleOptions? Options { get; set; }

		[Parameter]
		public EventCallback<TradeConsoleOptions> OnOptionsChanged { get; set; }

		[Parameter]
		public bool HasSelection { get; set; }

		[Parameter]
		public SymbolTradingLimits? Limits { get; set; }

		[Parameter]
		public double? Balance { get; set; }

		[Parameter]
		public double? Leverage { get; set; }

		[Parameter]
		public double PipSize { get; set; } = 0.01;

		[Parameter]
		public double? CurrentPrice { get; set; }

		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		[Parameter, EditorRequired]
		public RobotRuleSet OpenLongRules { get; set; } = new();

		[Parameter, EditorRequired]
		public RobotRuleSet OpenShortRules { get; set; } = new();

		[Parameter, EditorRequired]
		public RobotRuleSet CloseRules { get; set; } = new();

		/// <summary>Raised after any rule set is edited, so the host can persist it.</summary>
		[Parameter]
		public EventCallback OnRulesChanged { get; set; }

		/// <summary>Evaluates the rule-engine signals a rule set asks for (its Top % cutoff and active groups) -- null while no simulation is loaded.</summary>
		[Parameter]
		public Func<RobotRuleSet, RuleEngineResult?> ComputeRuleEngine { get; set; } = _ => null;

		[Parameter]
		public DateTime? CurrentTimestampUtc { get; set; }

		[Parameter]
		public string? TxFoldTable { get; set; }

		[Parameter]
		public DateTime? TxFoldAnchorEnd { get; set; }

		[Parameter]
		public DateTime? TxFoldRangeStart { get; set; }

		[Parameter]
		public HashSet<int> TxFoldSelectedSeconds { get; set; } = new();

		private Task SetEvaluationSeconds(ChangeEventArgs e) =>
			double.TryParse(e.Value?.ToString(), out var seconds)
				? OnEvaluationSecondsChanged.InvokeAsync(Math.Max(0, seconds))
				: Task.CompletedTask;

		private Task SelectSubTab(RobotSubTab tab) => OnSubTabChanged.InvokeAsync(tab);

		private Task SelectOpenSide(RobotOpenSide side) => OnOpenSideChanged.InvokeAsync(side);

		private Task SetXGridPercent(RobotRuleSet rules, double percent)
		{
			rules.XGridPercent = percent;
			return OnRulesChanged.InvokeAsync();
		}
	}
}
