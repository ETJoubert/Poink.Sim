using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The broker's fund, what has been paid into it, lending and absorbed losses alongside the market's trade, volume and
	/// order counts, with a button to replenish the fund.
	/// </summary>
	public partial class EmulatorBrokerComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public BrokerSummary Broker { get; set; } = default!;

		[Parameter]
		public double FundTopUp { get; set; }

		[Parameter]
		public EventCallback OnReplenishFund { get; set; }

		private string FundUsePercent =>
			Broker.FundBalance > 0 ? (Broker.Lent / Broker.FundBalance).ToString("P1", CultureInfo.InvariantCulture) : "all";
	}
}
