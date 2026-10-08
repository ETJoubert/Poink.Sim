using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Side selection, close-result summaries and volume snapping for the Trade page's quick trade console.</summary>
	public static class QuickTradeHelper
	{
		public static readonly IReadOnlyList<double> VolumePresets = [0.01, 0.1, 1];

		public static bool IsVolumeAllowed(double volume, double? minVolume) =>
			minVolume is not { } min || volume >= min - 1e-9;

		/// <summary>The stored preset when the symbol allows it, else the smallest preset it allows; null when none are allowed.</summary>
		public static double? ChooseVolume(double? stored, IReadOnlyList<double> presets, double? minVolume)
		{
			if (stored is { } volume && presets.Contains(volume) && IsVolumeAllowed(volume, minVolume))
				return volume;

			return presets.Where(p => IsVolumeAllowed(p, minVolume)).Cast<double?>().FirstOrDefault();
		}

		/// <summary>Margin a new order commits, priced at the mid quote and the account's leverage; null until both are known.</summary>
		public static double? OrderMargin(double volume, double? bid, double? ask, double? leverage) =>
			bid is { } b && ask is { } a && leverage is > 0 ? RiskHelper.MarginRequired(volume, (b + a) / 2, leverage.Value) : null;

		/// <summary>Free margin once <paramref name="orderMargin"/> is committed; negative means the order does not fit.</summary>
		public static double? MarginFreeAfter(double? availableMargin, double? orderMargin) =>
			availableMargin is { } available && orderMargin is { } margin ? available - margin : null;

		/// <summary>
		/// Auto-fire places an order only while <paramref name="allowed"/> (auto-fire on, ARMED, a chart open), on the zone
		/// gate's locked-to-open change, for a flat and idle side with a volume to send.
		/// </summary>
		public static bool ShouldAutoFire(bool allowed, bool wasLocked, ColumnZoneGate gate, QuickTradeSideState side, double orderVolume) =>
			allowed
			&& gate.Enabled
			&& wasLocked
			&& !gate.IsLocked(side.IsLong)
			&& !side.HasOpenPositions
			&& !side.Busy
			&& orderVolume > 0;

		public static bool IsReported(QuickTradeAwaitedReport awaited, int currentCount) =>
			awaited.IsOpening ? currentCount > awaited.BaselineCount : currentCount == 0;

		public static bool IsAwaitingReport(QuickTradeAwaitedReport awaited, int currentCount, DateTime nowUtc) =>
			nowUtc < awaited.DeadlineUtc && !IsReported(awaited, currentCount);

		public static IReadOnlyList<CTraderPositionInfo> SidePositions(
			IEnumerable<CTraderPositionInfo> positions,
			long symbolId,
			bool isLong
		) => positions.Where(p => p.SymbolId == symbolId && p.IsLong == isLong).ToList();

		public static CTraderOrderResult SummarizeClose(bool isLong, IReadOnlyList<CTraderOrderResult> results)
		{
			var side = isLong ? "long" : "short";
			var failures = results.Where(r => !r.Success).Select(r => r.Message).ToList();
			if (failures.Count == 0)
				return new CTraderOrderResult(true, $"Closed {results.Count} {side} position(s).");

			var closed = results.Count - failures.Count;
			return new CTraderOrderResult(
				false,
				$"Closed {closed} of {results.Count} {side} position(s): {string.Join("; ", failures)}"
			);
		}

		/// <summary>Snaps a volume onto the symbol's step and lifts it to the symbol's minimum; both are optional while metadata loads.</summary>
		public static double SnapVolume(double volume, double? minVolume, double? volumeStep)
		{
			var snapped = volumeStep is > 0
				? Math.Round(volume / volumeStep.Value, MidpointRounding.AwayFromZero) * volumeStep.Value
				: volume;
			snapped = Math.Round(snapped, 8);
			return minVolume is { } min && snapped < min ? min : snapped;
		}
	}
}
