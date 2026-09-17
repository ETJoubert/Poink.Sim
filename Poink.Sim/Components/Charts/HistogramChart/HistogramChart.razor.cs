using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>Public because a [Parameter] can't expose a less-accessible type; callers convert from their own internal bin model.</summary>
	public readonly record struct HistogramBar(double Low, double High, long Count);

	/// <summary>One overlay series for the multi-series view -- Bins must share the same bucket edges as its siblings.</summary>
	public readonly record struct HistogramSeries(string Name, string Color, IReadOnlyList<HistogramBar> Bins);

	public partial class HistogramChart : ComponentBase
	{
		[Parameter]
		public IReadOnlyList<HistogramBar> Bins { get; set; } = Array.Empty<HistogramBar>();

		/// <summary>When set, overrides Bins entirely and renders a legend plus one colored bar per series per bucket.</summary>
		[Parameter]
		public IReadOnlyList<HistogramSeries>? Series { get; set; }

		[Parameter]
		public string Title { get; set; } = "DISTRIBUTION";

		[Parameter]
		public string ValueUnit { get; set; } = "";

		[Parameter]
		public double ValueScale { get; set; } = 1;

		[Parameter]
		public string EmptyMessage { get; set; } = "no data yet";

		/// <summary>Total samples the multi-series run was computed over; drives Grand % when set. Falls back to the summed series bins when omitted.</summary>
		[Parameter]
		public long? SampleCount { get; set; }

		private long TotalCount => Bins.Sum(b => b.Count);

		private long MaxCount => Bins.Count == 0 ? 0 : Bins.Max(b => b.Count);

		private int SeriesBucketCount => Series?.Count > 0 ? Series[0].Bins.Count : 0;

		private long SeriesMaxCount => Series is { Count: > 0 } s ? s.SelectMany(x => x.Bins).Select(b => b.Count).DefaultIfEmpty(0).Max() : 0;

		private string FormatRange(HistogramBar bin) =>
			$"{(bin.Low * ValueScale).ToString("0.#", CultureInfo.InvariantCulture)}-{(bin.High * ValueScale).ToString("0.#", CultureInfo.InvariantCulture)}{ValueUnit}";

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private static string BarWidthPercent(long count, long maxCount) =>
			(maxCount == 0 ? 0 : count * 100.0 / maxCount).ToString("0.##", CultureInfo.InvariantCulture);

		private string PercentOfTotalText(long count) => (TotalCount == 0 ? 0 : count * 100.0 / TotalCount).ToString("N1");

		private static long SeriesTotal(HistogramSeries series) => series.Bins.Sum(b => b.Count);

		private static string PercentOfSeriesTotalText(long count, long seriesTotal) =>
			(seriesTotal == 0 ? 0 : count * 100.0 / seriesTotal).ToString("N1");

		// SampleCount reflects the run's actual sample size; summed series bins would double-count across metrics.
		private long GrandTotal => SampleCount ?? (Series is { Count: > 0 } s ? s.Sum(SeriesTotal) : 0);

		private static string PercentOfGrandTotalText(long count, long grandTotal) =>
			(grandTotal == 0 ? 0 : count * 100.0 / grandTotal).ToString("N1");
	}
}
