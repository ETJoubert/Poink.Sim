using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>Vertical price span an Emulator chart is scaled to.</summary>
internal readonly record struct EmulatorPriceRange(double Min, double Max)
{
	public double Span => Max - Min;
}

/// <summary>Maps the Emulator's prices and book volumes onto chart and depth-bar geometry.</summary>
internal static class EmulatorChartHelper
{
	private const double RangePaddingFraction = 0.08;

	/// <summary>A span centred on the current price, wide enough to fit the whole history on either side.</summary>
	public static EmulatorPriceRange RangeOf(IReadOnlyList<EmulatorPricePoint> history, double price, double tickSize)
	{
		var reach = history.Count > 0 ? history.Max(p => Math.Abs(p.Price - price)) : 0;
		var halfSpan = Math.Max(reach * (1 + RangePaddingFraction), tickSize);
		return new EmulatorPriceRange(price - halfSpan, price + halfSpan);
	}

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
