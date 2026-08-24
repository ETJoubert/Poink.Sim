using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Components.Training
{
	// Generation-indexed sibling of RewardChart -- NEAT emits one stats record
	// per generation (already aggregated), so this plots raw points with no
	// rolling window, unlike TrendLineChart's per-episode smoothing.
	public partial class NeatEvolutionChart : ComponentBase
	{
		private const double ViewWidth = 600;
		private const double ViewHeight = 160;
		private const double PadLeft = 8;
		private const double PadRight = 8;
		private const double PadTop = 10;
		private const double PadBottom = 10;

		[Parameter]
		public IReadOnlyList<NeatGenerationStats> Generations { get; set; } = Array.Empty<NeatGenerationStats>();

		[Parameter]
		public bool IsRunning { get; set; }

		[Parameter]
		public Func<NeatGenerationStats, double> PrimarySelector { get; set; } = default!;

		[Parameter]
		public string PrimaryLabel { get; set; } = "value";

		[Parameter]
		public Func<NeatGenerationStats, double>? SecondarySelector { get; set; }

		[Parameter]
		public string? SecondaryLabel { get; set; }

		[Parameter]
		public string ColorVariant { get; set; } = "fitness";

		[Parameter]
		public string ValueFormat { get; set; } = "N2";

		private (double Min, double Max) _valueRange = (-1, 1);

		private sealed record ChartPoint(string X, string Y, int Generation, double Value, bool IsLatest);

		private static double PlotWidth => ViewWidth - PadLeft - PadRight;
		private static double PlotHeight => ViewHeight - PadTop - PadBottom;

		protected override void OnParametersSet() => _valueRange = ComputeValueRange();

		private (double Min, double Max) ComputeValueRange()
		{
			if (Generations.Count == 0)
				return (-1, 1);

			var values = Generations.Select(PrimarySelector);
			if (SecondarySelector is not null)
				values = values.Concat(Generations.Select(SecondarySelector));

			double min = Math.Min(0, values.Min());
			double max = Math.Max(0, values.Max());
			if (Math.Abs(max - min) < 1e-6)
			{
				min -= 1;
				max += 1;
			}
			return (min, max);
		}

		private double MapX(int index) =>
			Generations.Count <= 1 ? PadLeft + PlotWidth / 2 : PadLeft + index * (PlotWidth / (Generations.Count - 1));

		private double MapY(double value)
		{
			var (min, max) = _valueRange;
			double t = (value - min) / (max - min);
			return PadTop + (1 - t) * PlotHeight;
		}

		private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

		private string BaselineY => FormatCoord(MapY(0));

		private (double Min, double Max) ValueRange => _valueRange;

		private List<ChartPoint> BuildPoints(Func<NeatGenerationStats, double> selector) =>
			Generations
				.Select(
					(g, i) =>
						new ChartPoint(
							FormatCoord(MapX(i)),
							FormatCoord(MapY(selector(g))),
							g.Generation,
							selector(g),
							i == Generations.Count - 1
						)
				)
				.ToList();

		private List<ChartPoint> PrimaryPoints => BuildPoints(PrimarySelector);
		private List<ChartPoint>? SecondaryPoints => SecondarySelector is null ? null : BuildPoints(SecondarySelector);

		private static string PolylinePoints(List<ChartPoint> points) =>
			string.Join(" ", points.Select(p => $"{p.X},{p.Y}"));

		private string FormatValue(double v) => v.ToString(ValueFormat, CultureInfo.InvariantCulture);
	}
}
