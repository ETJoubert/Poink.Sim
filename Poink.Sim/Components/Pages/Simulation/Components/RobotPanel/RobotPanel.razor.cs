using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Shared.TradeConsole;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation page's Robot tab: Positioning, Open and Close sub-tabs. Positioning reuses
	/// <see cref="TradeConsole"/> in settings mode to capture the robot's position options; Open and
	/// Close are placeholders until their rules exist.
	/// </summary>
	public partial class RobotPanel : ComponentBase
	{
		private enum RobotSubTab
		{
			Positioning,
			Open,
			Close,
		}

		private RobotSubTab _activeTab;

		[Parameter]
		public TradeConsoleOptions? Options { get; set; }

		[Parameter]
		public EventCallback<TradeConsoleOptions> OnOptionsChanged { get; set; }

		[Parameter]
		public bool HasSelection { get; set; }

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
	}
}
