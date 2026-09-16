using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	// Public because a [Parameter] can't expose a less-accessible type; callers convert from their own internal bin model.
	public readonly record struct HistogramBar(double Low, double High, long Count);

	public partial class HistogramChart : ComponentBase
	{
		[Parameter]
		public IReadOnlyList<HistogramBar> Bins { get; set; } = Array.Empty<HistogramBar>();

		[Parameter]
		public string Title { get; set; } = "DISTRIBUTION";

		[Parameter]
		public string ValueUnit { get; set; } = "";

		[Parameter]
		public double ValueScale { get; set; } = 1;

		[Parameter]
		public string EmptyMessage { get; set; } = "no data yet";

		private long TotalCount => Bins.Sum(b => b.Count);

		private long MaxCount => Bins.Count == 0 ? 0 : Bins.Max(b => b.Count);

		private string FormatRange(HistogramBar bin) =>
			$"{(bin.Low * ValueScale).ToString("0.#", CultureInfo.InvariantCulture)}-{(bin.High * ValueScale).ToString("0.#", CultureInfo.InvariantCulture)}{ValueUnit}";

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private static string BarWidthPercent(long count, long maxCount) =>
			(maxCount == 0 ? 0 : count * 100.0 / maxCount).ToString("0.##", CultureInfo.InvariantCulture);
	}
}
