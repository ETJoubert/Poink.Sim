using System;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>Live readiness of the OrderBot bridge for the selected account, refreshed in the background.</summary>
	internal interface IOrderBotStatus
	{
		OrderBotStatus Status { get; }

		event Action? Changed;
	}
}
