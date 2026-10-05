using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>How <see cref="CompareChart"/>'s main pane scales its series.</summary>
	public enum CompareChartScaleMode
	{
		/// <summary>Raw values, shown as % change from each series' first visible point -- re-bases as the view pans.</summary>
		Rebased,

		/// <summary>Values that are already percentages, plotted as-is on a % axis.</summary>
		PercentValues,
	}

	/// <summary>How <see cref="CompareChart"/> draws main-pane series that carry bars; lower-pane series are always lines.</summary>
	public enum CompareChartSeriesStyle
	{
		Lines,

		/// <summary>Candles in the series' colour (up filled, down hollow) at a fixed partial opacity so overlaps stay readable.</summary>
		Bars,
	}

	/// <summary>
	/// One series on a <see cref="CompareChart"/>; <paramref name="Pane"/> 0 is the main pane, 1 the lower pane.
	/// <paramref name="Bars"/> is drawn instead of <paramref name="Points"/> in <see cref="CompareChartSeriesStyle.Bars"/>.
	/// </summary>
	public sealed record CompareChartSeries(
		string Label,
		string ColorVar,
		IReadOnlyList<ChartLinePoint> Points,
		int Pane = 0,
		IReadOnlyList<CTraderBarRow>? Bars = null
	);
}
