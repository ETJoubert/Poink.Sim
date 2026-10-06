using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>One symbol's bars as the Trade compare XGrid reads them.</summary>
	internal sealed record TradeCompareXGridSource(
		string Table,
		string ColorVar,
		IReadOnlyList<HlocAvdtBar> Bars,
		IReadOnlyList<BarPriceHistogram> Histograms
	);

	/// <summary>Pure anchor and XGrid math for the Trade chart's TxFold and compare views.</summary>
	internal static class TradeCompareHelper
	{
		/// <summary>
		/// The bar TxFold anchors just past: the window's last bar, except at offset 0 where that bar is still forming, so
		/// the last completed one is used and the anchor (and TxFold's database read) moves once per bar, not per tick.
		/// </summary>
		public static HlocAvdtBar? TxFoldAnchorBar(IReadOnlyList<HlocAvdtBar> windowedBars, int clampedOffset) =>
			windowedBars.Count switch
			{
				0 => null,
				1 => windowedBars[0],
				_ => clampedOffset == 0 ? windowedBars[^2] : windowedBars[^1],
			};

		/// <summary>The close at or before <paramref name="anchor"/>, else the first close inside the window, so a symbol whose data starts later still gets a reference.</summary>
		public static double? ReferenceClose(IReadOnlyList<HlocAvdtBar> bars, DateTime anchor, DateTime windowStart)
		{
			var atOrBefore = -1;
			for (var i = 0; i < bars.Count && bars[i].OpenTime <= anchor; i++)
				atOrBefore = i;
			if (atOrBefore >= 0 && bars[atOrBefore].Close != 0)
				return bars[atOrBefore].Close;

			for (var i = 0; i < bars.Count; i++)
				if (bars[i].OpenTime >= windowStart)
					return bars[i].Close != 0 ? bars[i].Close : null;
			return null;
		}

		public static CompareXGridResult? ComputeXGrid(
			IReadOnlyList<TradeCompareXGridSource> sources,
			DateTime windowStart,
			DateTime windowEnd,
			DateTime anchor,
			double rowPercent,
			int colCount
		)
		{
			var inputs = sources
				.Select(source => (source, Reference: ReferenceClose(source.Bars, anchor, windowStart)))
				.Where(x => x.Reference is not null)
				.Select(x => new CompareXGridInput(x.source.Table, x.source.ColorVar, x.source.Bars, x.source.Histograms, x.Reference!.Value))
				.ToList();
			return CompareXGridStats.Compute(inputs, windowStart, windowEnd, rowPercent, colCount);
		}
	}
}
