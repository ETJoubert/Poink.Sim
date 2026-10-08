using System;
using System.Collections.Generic;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// The quick console's close helpers. Armed sides close each position on its own once price is back to its entry or
	/// better; a side disarms itself once it has no positions left or a close fails, and every side disarms when the
	/// selected account changes. The close automation adds the same breakeven watch per position after t seconds and a
	/// market time stop after t + s seconds, for the one symbol it manages. Scoped to the circuit -- the Trade page keeps
	/// armed sides in the tab's session storage and re-arms them after a reload.
	/// </summary>
	internal interface IPositionCloseService
	{
		/// <summary>Raised when a side is armed or disarmed; may fire off the render thread.</summary>
		event Action? Changed;

		/// <summary>Raised with the outcome of every close this service requests; may fire off the render thread.</summary>
		event Action<CTraderOrderResult>? CloseReported;

		IReadOnlyList<BreakevenSide> ArmedSides { get; }

		bool IsArmed(BreakevenSide side);

		/// <summary>Arms the side and at once closes any of its positions already at breakeven or better.</summary>
		void Arm(BreakevenSide side);

		void Disarm(BreakevenSide side);

		/// <summary>Sets the close automation and the symbol it manages, or turns it off with null.</summary>
		void SetAutomation(CloseAutomationScope? scope);
	}
}
