using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// Each group's total equity over the market's history as one line per group, in the group's colour, on a shared
	/// scale, with a legend of the latest values.
	/// </summary>
	public partial class EmulatorEquityCurveComponent : ComponentBase
	{
		private const double ViewWidth = 1000;
		private const double ViewHeight = 300;

		[Parameter, EditorRequired]
		public IReadOnlyList<EmulatorEquityPoint> History { get; set; } = Array.Empty<EmulatorEquityPoint>();

		/// <summary>The groups in their settings order, which fixes each line's colour and the legend order.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<PopulationSettings> Groups { get; set; } = Array.Empty<PopulationSettings>();

		private IReadOnlyList<EquityLine> _lines = Array.Empty<EquityLine>();
		private EmulatorPriceRange _range;

		protected override void OnParametersSet()
		{
			var series = Groups
				.Select((group, index) => (group.Name, Color: EmulatorGroupColorHelper.ColorVar(index), Series: SeriesOf(group.Name)))
				.Where(s => s.Series.Values.Count > 0)
				.ToArray();
			_range = EmulatorChartHelper.RangeOfValues(series.SelectMany(s => s.Series.Values));
			_lines = series
				.Select(s => new EquityLine(
					s.Name,
					s.Color,
					EmulatorChartHelper.PolylinePoints(s.Series.Values, s.Series.FirstIndex, History.Count, _range, ViewWidth, ViewHeight),
					s.Series.Values[^1]
				))
				.ToArray();
		}

		// A group added mid-run has no equity before it joined, so its line starts where it first appears.
		private (int FirstIndex, IReadOnlyList<double> Values) SeriesOf(string group)
		{
			var firstIndex = History.ToList().FindIndex(point => point.EquityByGroup.ContainsKey(group));
			if (firstIndex < 0)
			{
				return (0, Array.Empty<double>());
			}

			var values = History.Skip(firstIndex).Select(point => point.EquityByGroup.GetValueOrDefault(group)).ToArray();
			return (firstIndex, values);
		}

		private sealed record EquityLine(string Name, string Color, string Points, double Latest);
	}
}
