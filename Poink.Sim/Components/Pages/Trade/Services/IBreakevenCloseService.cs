using System;
using System.Collections.Generic;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Watches armed sides tick by tick and closes each of their positions on its own as soon as price is back to its
	/// entry or better. A side disarms itself once it has no positions left, or when a close fails; every side disarms
	/// when the selected account changes. Scoped to the circuit -- the Trade page keeps the armed sides in the tab's
	/// session storage and re-arms them after a reload.
	/// </summary>
	internal interface IBreakevenCloseService
	{
		IReadOnlyList<BreakevenSide> ArmedSides { get; }

		/// <summary>Raised when a side is armed or disarmed; may fire off the render thread.</summary>
		event Action? Changed;

		/// <summary>Raised with the outcome of every close this service requests; may fire off the render thread.</summary>
		event Action<CTraderOrderResult>? CloseReported;

		bool IsArmed(BreakevenSide side);

		/// <summary>Arms the side and at once closes any of its positions already at breakeven or better.</summary>
		void Arm(BreakevenSide side);

		void Disarm(BreakevenSide side);
	}
}
