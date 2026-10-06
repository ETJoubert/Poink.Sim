using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The broker's fund, lending and absorbed losses alongside the market's trade, volume and order counts.
	/// </summary>
	public partial class EmulatorBrokerComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public BrokerSummary Broker { get; set; } = default!;

		private string FundUsePercent =>
			Broker.FundBalance > 0 ? (Broker.Lent / Broker.FundBalance).ToString("P1", CultureInfo.InvariantCulture) : "all";
	}
}
