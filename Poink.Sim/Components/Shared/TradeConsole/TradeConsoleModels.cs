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

	public enum SizingMode
	{
		Pips,
		PercentBalance,
	}

	public enum VolumeSizingMode
	{
		Manual,
		Risk,
	}

	/// <summary>
	/// Price-independent position settings captured from TradeConsole in settings mode -- sizing and
	/// SL/TP expressed as pips or percent of balance rather than absolute prices, so they stay valid
	/// whatever price a position is eventually opened at.
	/// </summary>
	public sealed record TradeConsoleOptions(
		bool IsLong,
		VolumeSizingMode VolumeMode,
		double? VolumeUnits,
		double? RiskPercent,
		SizingMode StopLossMode,
		double? StopLossPips,
		double? StopLossPercent,
		SizingMode TakeProfitMode,
		double? TakeProfitPips,
		double? TakeProfitPercent
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
