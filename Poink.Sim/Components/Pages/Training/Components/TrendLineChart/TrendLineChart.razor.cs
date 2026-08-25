using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	/// <summary>
	/// Generic rolling-window scatter chart for a per-episode statistic; callers supply only
	/// how to reduce a window of episodes to one number (Aggregate). Points only, no
	/// connecting line/fill, so trend is read from the point cloud's shape.
	/// </summary>
	public partial class TrendLineChart : ComponentBase
	{
		private const double ViewWidth = 600;
		private const double ViewHeight = 160;
		private const double PadLeft = 8;
		private const double PadRight = 8;
		private const double PadTop = 10;
		private const double PadBottom = 10;

		private static readonly int[] WindowOptions = { 10, 20, 50, 100 };

		[Parameter]
		public IReadOnlyList<RlEpisodeMetric> Episodes { get; set; } = Array.Empty<RlEpisodeMetric>();

		[Parameter]
		public bool IsRunning { get; set; }

		/// <summary>Reduces a trailing window of episodes to the one number this chart plots -- e.g. median/population-stddev of Reward, or win rate %.</summary>
		[Parameter]
		public Func<IReadOnlyList<RlEpisodeMetric>, double> Aggregate { get; set; } = default!;

		/// <summary>CSS modifier selecting the line/point color -- see TrendLineChart.razor.css.</summary>
		[Parameter]
		public string ColorVariant { get; set; } = "median";

		[Parameter]
		public string ValueFormat { get; set; } = "N2";

		[Parameter]
		public string ValueSuffix { get; set; } = "";

		[Parameter]
		public string EmptyMessage { get; set; } = "no episodes yet";

		// 20 episodes.
		private int _windowIndex = 1;

		// Recomputed once per render rather than as get-only properties: recomputing per Points
		// read (up to 500/render) turned an O(n*w) pass into O(n^2*w), real CPU competing with training.
		private List<(int Episode, double Value)> _series = new();
		private (double Min, double Max) _valueRange = (-1, 1);

		private int WindowSize => WindowOptions[Math.Clamp(_windowIndex, 0, WindowOptions.Length - 1)];

		private sealed record ChartPoint(string X, string Y, int Episode, double Value, bool IsLatest);

		private static double PlotWidth => ViewWidth - PadLeft - PadRight;
		private static double PlotHeight => ViewHeight - PadTop - PadBottom;

		protected override void OnParametersSet() => Recompute();

		private void Recompute()
		{
			_series = BuildTrendSeries();
			_valueRange = ComputeValueRange(_series);
		}

		// A partial trailing window is only allowed when Episodes[0] is truly episode 1; once
		// history has evicted its oldest entry, a short window there is skipped instead of drifting.
		private List<(int Episode, double Value)> BuildTrendSeries()
		{
			if (Episodes.Count == 0)
				return new List<(int, double)>();

			int window = WindowSize;
			bool retainsFullHistory = Episodes[0].EpisodeNumber == 1;
			var series = new List<(int, double)>(Episodes.Count);
			for (int i = 0; i < Episodes.Count; i++)
			{
				int start = i - window + 1;
				if (start < 0)
				{
					if (!retainsFullHistory)
						continue;
					start = 0;
				}

				var slice = new List<RlEpisodeMetric>(i - start + 1);
				for (int j = start; j <= i; j++)
					slice.Add(Episodes[j]);
				series.Add((Episodes[i].EpisodeNumber, Aggregate(slice)));
			}
			return series;
		}

		// Zero is always folded into the range so the baseline is a fixed anchor, rather than
		// an auto-zoomed window that makes small fluctuations near a nonzero value look huge.
		private static (double Min, double Max) ComputeValueRange(List<(int Episode, double Value)> series)
		{
			if (series.Count == 0)
				return (-1, 1);
			double min = Math.Min(0, series.Min(p => p.Value));
			double max = Math.Max(0, series.Max(p => p.Value));
			if (Math.Abs(max - min) < 1e-6)
			{
				min -= 1;
				max += 1;
			}
			return (min, max);
		}

		private double MapX(int index, int count) =>
			count <= 1 ? PadLeft + PlotWidth / 2 : PadLeft + index * (PlotWidth / (count - 1));

		private double MapY(double value)
		{
			var (min, max) = _valueRange;
			double t = (value - min) / (max - min);
			return PadTop + (1 - t) * PlotHeight;
		}

		private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

		private string BaselineY => FormatCoord(MapY(0));

		private (double Min, double Max) ValueRange => _valueRange;

		private List<ChartPoint> Points =>
			_series
				.Select(
					(p, i) =>
						new ChartPoint(
							FormatCoord(MapX(i, _series.Count)),
							FormatCoord(MapY(p.Value)),
							p.Episode,
							p.Value,
							i == _series.Count - 1
						)
				)
				.ToList();

		private string FormatValue(double v) => v.ToString(ValueFormat, CultureInfo.InvariantCulture) + ValueSuffix;

		private void OnWindowSizeChanged(ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
			{
				int idx = Array.IndexOf(WindowOptions, size);
				if (idx >= 0)
					_windowIndex = idx;
			}
			Recompute();
		}
	}
}
