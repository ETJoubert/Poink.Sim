using Poink.Sim.Models;

namespace Poink.Sim.Components.Shared.TradeConsole
{
	/// <summary>A validated order ready to submit -- the host page turns this into whatever its own backend needs.</summary>
	public readonly record struct TradeConsoleOrderRequest(
		bool IsLong,
		double Volume,
		OrderType OrderType,
		double? TriggerPrice,
		double? StopLossPrice,
		double? TakeProfitPrice
	);

	/// <summary>A not-yet-submitted order being edited in TradeConsole, for a host page to preview elsewhere (e.g. on a chart).</summary>
	public readonly record struct TradeConsoleDraft(
		bool IsLong,
		double EntryPrice,
		double Volume,
		double? StopLossPrice,
		double? TakeProfitPrice
	);
}
