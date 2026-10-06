using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>Vertical price span an Emulator chart is scaled to.</summary>
public readonly record struct EmulatorPriceRange(double Min, double Max)
{
	public double Span => Max - Min;
}

/// <summary>Maps the Emulator's prices and book volumes onto chart and depth-bar geometry.</summary>
internal static class EmulatorChartHelper
{
	public static double YFraction(double price, EmulatorPriceRange range) =>
		range.Span <= 0 ? 0.5 : Math.Clamp(1 - (price - range.Min) / range.Span, 0, 1);

	public static string PolylinePoints(
		IReadOnlyList<EmulatorPricePoint> history,
		EmulatorPriceRange range,
		double width,
		double height
	)
	{
		if (history.Count == 0)
		{
			return string.Empty;
		}

		var xStep = history.Count > 1 ? width / (history.Count - 1) : 0;
		return string.Join(
			' ',
			history.Select((point, index) => $"{Coord(index * xStep)},{Coord(YFraction(point.Price, range) * height)}")
		);
	}

	/// <summary>
	/// Folds history points into bars of <paramref name="barSpan"/>, aligned to the clock so a bar keeps its bounds as the
	/// history scrolls.
	/// </summary>
	public static IReadOnlyList<EmulatorPricePoint> GroupBars(IReadOnlyList<EmulatorPricePoint> history, TimeSpan barSpan) =>
		history
			.GroupBy(point => point.Timestamp.Ticks / Math.Max(1, barSpan.Ticks))
			.Select(bar => new EmulatorPricePoint(
				bar.First().Timestamp,
				bar.First().Open,
				bar.Max(point => point.High),
				bar.Min(point => point.Low),
				bar.Last().Price
			))
			.ToArray();

	/// <summary>
	/// SVG path data for OHLC bars spread across the width: a high-low stem, the open ticked left and the close ticked
	/// right, split into rising and falling bars so each can take its own colour.
	/// </summary>
	public static (string Rising, string Falling) OhlcPaths(
		IReadOnlyList<EmulatorPricePoint> bars,
		EmulatorPriceRange range,
		double width,
		double height
	)
	{
		var rising = new StringBuilder();
		var falling = new StringBuilder();
		var slot = bars.Count > 0 ? width / bars.Count : 0;
		var tick = slot * 0.35;
		for (var i = 0; i < bars.Count; i++)
		{
			var bar = bars[i];
			var x = (i + 0.5) * slot;
			(bar.Price >= bar.Open ? rising : falling).Append(
				$"M{Coord(x)},{Y(bar.High)}V{Y(bar.Low)}M{Coord(x - tick)},{Y(bar.Open)}H{Coord(x)}M{Coord(x)},{Y(bar.Price)}H{Coord(x + tick)}"
			);
		}

		return (rising.ToString(), falling.ToString());

		string Y(double price) => Coord(YFraction(price, range) * height);
	}

	public static double DepthPercent(double volume, double maxVolume) =>
		maxVolume <= 0 ? 0 : Math.Clamp(volume / maxVolume * 100, 0, 100);

	public static string PriceFormat(double tickSize) =>
		"F" + Math.Max(0, (int)Math.Ceiling(-Math.Log10(tickSize) - 1e-9)).ToString(CultureInfo.InvariantCulture);

	public static string CompactAmount(double amount) =>
		Math.Abs(amount) switch
		{
			>= 1_000_000 => (amount / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "M",
			>= 1_000 => (amount / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k",
			_ => amount.ToString("0", CultureInfo.InvariantCulture),
		};

	public static string Coord(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
