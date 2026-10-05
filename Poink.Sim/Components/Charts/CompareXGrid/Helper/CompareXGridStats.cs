using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>One symbol's loaded bars and their price histograms, with the price its % rows are measured from.</summary>
	internal sealed record CompareXGridInput(
		string Label,
		string ColorVar,
		IReadOnlyList<HlocAvdtBar> Bars,
		IReadOnlyList<BarPriceHistogram> Histograms,
		double ReferencePrice
	);

	/// <summary>
	/// Builds a <see cref="CompareXGridResult"/>: every symbol's histogram ticks inside [start, end] binned into % rows
	/// from its own reference price and into equal time columns, so differently priced symbols line up cell for cell.
	/// </summary>
	internal static class CompareXGridStats
	{
		// Rows past this are dropped from the edges rather than shrinking the row size to fit.
		public const int MaxRows = 400;

		public static CompareXGridResult? Compute(
			IReadOnlyList<CompareXGridInput> inputs,
			DateTime start,
			DateTime end,
			double rowPercent,
			int colCount
		)
		{
			if (inputs.Count == 0 || end < start || rowPercent <= 0)
				return null;
			colCount = Math.Max(1, colCount);

			var binned = inputs.Select(input => Bin(input, start, end, rowPercent, colCount)).ToList();
			var cells = binned.SelectMany(b => b.Keys).ToList();
			if (cells.Count == 0)
				return null;

			var topBand = cells.Max(c => c.Band);
			var bottomBand = Math.Max(cells.Min(c => c.Band), topBand - MaxRows + 1);
			var rowCount = (int)(topBand - bottomBand + 1);

			var symbols = inputs
				.Zip(binned, (input, b) =>
				{
					var counts = new int[rowCount * colCount];
					foreach (var ((band, col), count) in b)
						if (band >= bottomBand)
							counts[(int)(topBand - band) * colCount + col] += count;
					return new CompareXGridSymbol(input.Label, input.ColorVar, counts, counts.Length == 0 ? 0 : counts.Max());
				})
				.ToList();

			var span = end - start;
			var columnStarts = Enumerable.Range(0, colCount).Select(c => start + span * c / colCount).ToList();
			return new CompareXGridResult(rowCount, colCount, (topBand + 1) * rowPercent, rowPercent, columnStarts, end, symbols);
		}

		// Buckets are placed by their midpoint; pip-sized buckets are far narrower than any useful % row, so straddling is negligible.
		private static Dictionary<(long Band, int Col), int> Bin(
			CompareXGridInput input,
			DateTime start,
			DateTime end,
			double rowPercent,
			int colCount
		)
		{
			var cells = new Dictionary<(long Band, int Col), int>();
			if (input.ReferencePrice <= 0)
				return cells;

			var spanTicks = Math.Max(1, (end - start).Ticks);
			for (var i = 0; i < input.Bars.Count && i < input.Histograms.Count; i++)
			{
				var openTime = input.Bars[i].OpenTime;
				if (openTime < start || openTime > end)
					continue;

				var col = (int)Math.Min(colCount - 1, (openTime - start).Ticks * colCount / spanTicks);
				var histogram = input.Histograms[i];
				foreach (var (key, bucket) in histogram.Buckets)
				{
					var midPrice = (key + 0.5) * histogram.BucketWidth;
					var band = (long)Math.Floor((midPrice / input.ReferencePrice - 1) * 100 / rowPercent);
					cells.TryGetValue((band, col), out var existing);
					cells[(band, col)] = existing + bucket.Count;
				}
			}
			return cells;
		}
	}
}
