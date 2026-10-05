using System.Collections.Generic;

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

	/// <summary>One line on a <see cref="CompareChart"/>; <paramref name="Pane"/> 0 is the main pane, 1 the lower pane.</summary>
	public sealed record CompareChartSeries(string Label, string ColorVar, IReadOnlyList<ChartLinePoint> Points, int Pane = 0);
}
