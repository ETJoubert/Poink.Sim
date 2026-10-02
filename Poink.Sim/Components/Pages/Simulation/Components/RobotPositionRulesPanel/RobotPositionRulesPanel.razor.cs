using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>Edits the robot's <see cref="RobotPositionRules"/>: stop and target modes, breakeven, trailing and drawdown sizing.</summary>
	public partial class RobotPositionRulesPanel : ComponentBase
	{
		[Parameter]
		public RobotPositionRules Rules { get; set; } = new();

		[Parameter]
		public EventCallback<RobotPositionRules> RulesChanged { get; set; }

		private Task SetNumber(ChangeEventArgs e, double min, Func<double, RobotPositionRules> apply) =>
			double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
				? RulesChanged.InvokeAsync(apply(Math.Max(min, value)))
				: Task.CompletedTask;

		private Task SetStopMode(ChangeEventArgs e) =>
			Enum.TryParse<RobotStopMode>(e.Value?.ToString(), out var mode)
				? RulesChanged.InvokeAsync(Rules with { StopMode = mode })
				: Task.CompletedTask;

		private Task SetTargetMode(ChangeEventArgs e) =>
			Enum.TryParse<RobotTargetMode>(e.Value?.ToString(), out var mode)
				? RulesChanged.InvokeAsync(Rules with { TargetMode = mode })
				: Task.CompletedTask;

		private Task ResetToDefaults() => RulesChanged.InvokeAsync(new RobotPositionRules());
	}
}
