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
	private const double ValueRangePaddingFraction = 0.08;

	public static double YFraction(double price, EmulatorPriceRange range) =>
		range.Span <= 0 ? 0.5 : Math.Clamp(1 - (price - range.Min) / range.Span, 0, 1);

	public static string PolylinePoints(
		IReadOnlyList<EmulatorPricePoint> history,
		EmulatorPriceRange range,
		double width,
		double height
	) => PolylinePoints(history.Select(point => point.Price).ToArray(), range, width, height);

	/// <summary>SVG polyline points for values spread evenly across the width, first value on the left.</summary>
	public static string PolylinePoints(IReadOnlyList<double> values, EmulatorPriceRange range, double width, double height) =>
		PolylinePoints(values, 0, values.Count, range, width, height);

	/// <summary>
	/// SVG polyline points for a series that starts <paramref name="firstIndex"/> slots into a timeline of
	/// <paramref name="totalCount"/> evenly spaced slots, so a series that began late still lines up in time.
	/// </summary>
	public static string PolylinePoints(
		IReadOnlyList<double> values,
		int firstIndex,
		int totalCount,
		EmulatorPriceRange range,
		double width,
		double height
	)
	{
		if (values.Count == 0)
		{
			return string.Empty;
		}

		var xStep = totalCount > 1 ? width / (totalCount - 1) : 0;
		return string.Join(
			' ',
			values.Select((value, index) => $"{Coord((firstIndex + index) * xStep)},{Coord(YFraction(value, range) * height)}")
		);
	}

	/// <summary>The span of <paramref name="values"/> with a little headroom above and below, never zero-height.</summary>
	public static EmulatorPriceRange RangeOfValues(IEnumerable<double> values)
	{
		var all = values.ToArray();
		if (all.Length == 0)
		{
			return new EmulatorPriceRange(0, 1);
		}

		var (min, max) = (all.Min(), all.Max());
		var padding = Math.Max((max - min) * ValueRangePaddingFraction, Math.Max(Math.Abs(max) * 0.001, 1));
		return new EmulatorPriceRange(min - padding, max + padding);
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
