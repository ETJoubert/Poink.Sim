using System.Linq;
using System.Text.Json;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// Hands the robot its options. The robot itself lives in the simulation engine; the page only keeps
	/// the engine's copy of the options current, pushing a fresh <see cref="RobotOptions"/> whenever any
	/// part of it -- switch, positioning, rules or the windows those rules read -- has changed.
	/// </summary>
	public partial class SimulationPage
	{
		private string? _lastRobotOptionsSignature;

		private RobotOptions BuildRobotOptions() =>
			new(
				_robotEnabled,
				_robotEvaluationSeconds,
				_robotPositionOptions ?? RobotPositionHelper.DefaultOptions,
				ToRobotRules(_openLongRules),
				ToRobotRules(_openShortRules),
				ToRobotRules(_closeRules),
				ClampedBarWindow,
				ClampedBarOffset,
				_xgridPipCount,
				_xgridColCount,
				_txFoldSelectedSeconds.OrderBy(s => s).ToHashSet()
			);

		private static RobotRules ToRobotRules(RobotRuleSet rules) =>
			new(rules.EnabledKeys.OrderBy(k => k).ToHashSet(), rules.XGridPercent);

		private void PushRobotOptionsIfChanged()
		{
			var options = BuildRobotOptions();
			var signature = JsonSerializer.Serialize(options);
			if (signature == _lastRobotOptionsSignature)
				return;

			_lastRobotOptionsSignature = signature;
			SimulationService.SetRobotOptions(options);
		}
	}
}
