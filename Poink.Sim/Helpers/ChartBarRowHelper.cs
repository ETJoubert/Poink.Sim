using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Helpers;

/// <summary>Projects aggregated bars into the CTraderBarRow/line-point shapes the market charts render.</summary>
internal static class ChartBarRowHelper
{
	public static List<CTraderBarRow> ToBarRows(IEnumerable<HlocAvdtBar> bars) =>
		bars.Select(b => new CTraderBarRow(
				b.OpenTime,
				b.Open,
				b.High,
				b.Low,
				b.Close,
				b.Volume,
				b.UpVolume,
				b.DownVolume,
				b.Distance
			))
			.ToList();

	/// <summary>Box plot mapping (Open/Close = Q1/Q3, High/Low = Min/Max), matching Analize's Price tab.</summary>
	public static List<CTraderBarRow> ToQuartileBarRows(IEnumerable<QuartileBar> bars) =>
		bars.Select(b => new CTraderBarRow(b.OpenTime, b.Q1, b.Max, b.Min, b.Q3, b.Count)).ToList();

	public static List<ChartLinePoint> ToMedianLine(IEnumerable<QuartileBar> bars) =>
		bars.Select(b => new ChartLinePoint(b.OpenTime, b.Median)).ToList();
}
