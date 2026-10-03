using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Poink.Sim.Components.Shared.IndicatorPanel;
using Poink.Sim.Models;

namespace Poink.Sim.Helpers;

/// <summary>Builds the indicator panel's rows from a run of completed bars, oldest first.</summary>
internal static class IndicatorRowsHelper
{
	private const string NoValue = "--";

	public static IReadOnlyList<IndicatorRow> Build(IReadOnlyList<HlocAvdtBar> completedBars, int window, double pipSize) =>
		[AtrRow(completedBars, window, pipSize)];

	// One bar more than the period is taken so the oldest true range still has a previous close to measure against.
	private static IndicatorRow AtrRow(IReadOnlyList<HlocAvdtBar> bars, int window, double pipSize)
	{
		var period = Math.Min(window, bars.Count);
		var label = $"ATR ({period})";
		if (period < 1 || pipSize <= 0)
			return new IndicatorRow(label, NoValue, "Average true range over the Lookback Window -- needs at least one completed bar");

		var tail = bars.Skip(Math.Max(0, bars.Count - period - 1)).ToList();
		double atr = MarketRegimeHelper.AtrSeries(tail, period)[^1];
		var pips = (atr / pipSize).ToString("F1", CultureInfo.InvariantCulture);
		var price = atr.ToString("F5", CultureInfo.InvariantCulture);
		return new IndicatorRow(label, $"{pips} pips", $"Average true range of the last {period} completed bars in the Lookback Window: {price} in price");
	}
}
