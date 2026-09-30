using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Charts;

internal readonly record struct TxFoldVolumeBucket(double Low, double High, int Count);

/// <summary>TrueHigh/TrueLow are the window's real min/max mid-price, independent of BarKind -- for
/// Quartile/Decile/MeanStdDev, Bar's own High/Low slots hold statistical bounds instead.</summary>
internal readonly record struct TxFoldBar(int LookbackSeconds, CTraderBarRow Bar, bool HasFullCoverage, double TrueHigh, double TrueLow);

/// <summary>Levels is null only when there are no ticks at all -- unlike Poc/Vah/Val inside it,
/// which can independently be null just from too little data for a meaningful value area.</summary>
internal readonly record struct TxFoldSnapshot(List<TxFoldBar> Bars, IReadOnlyList<TxFoldVolumeBucket> VolumeProfile, RangeStatsResult? Levels);

// Pure bar/coverage/volume-profile math over ticks the caller already fetched -- kept separate
// from TxFold.razor.cs, the same split XGridStats.Compute keeps from XGrid.razor.cs.
internal static class TxFoldStats
{
	// Windows overlap by design (each ends at anchorEnd), so BarKindChartService's fixed-epoch
	// bucketing can't be reused -- it would silently split an unaligned range across two buckets.
	public static TxFoldSnapshot Compute(
		IReadOnlyList<Tick> ticks,
		DateTime anchorEnd,
		DateTime rangeStart,
		IReadOnlyCollection<int> lookbackSeconds,
		BarAggregationKind kind
	)
	{
		var distinctSeconds = lookbackSeconds.Distinct().OrderBy(seconds => seconds).ToList();
		if (distinctSeconds.Count == 0)
			return new TxFoldSnapshot(new List<TxFoldBar>(), Array.Empty<TxFoldVolumeBucket>(), null);

		var bars = new List<TxFoldBar>(distinctSeconds.Count);
		foreach (var seconds in distinctSeconds)
		{
			var startIndex = LowerBound(ticks, anchorEnd.AddSeconds(-seconds));
			if (BuildBar(ticks, startIndex, kind) is { } result)
			{
				var hasFullCoverage = (anchorEnd - rangeStart).TotalSeconds > seconds;
				bars.Add(new TxFoldBar(seconds, result.Bar, hasFullCoverage, result.TrueHigh, result.TrueLow));
			}
		}

		var levels = RangeStatsHelper.Compute(ticks, t => (t.Bid + t.Ask) / 2.0);
		return new TxFoldSnapshot(bars, BuildVolumeProfile(ticks), levels);
	}

	// Fixed-length and bucket-count-scaled (% of that set's busiest bucket), not raw tick counts.
	public static float[] BuildVolumeProfileFeature(IReadOnlyList<Tick> ticks, int bucketCount)
	{
		if (bucketCount <= 0)
			return [];
		if (ticks.Count == 0)
			return new float[bucketCount];

		var buckets = RangeStatsHelper.BucketSamples(ticks, t => (t.Bid + t.Ask) / 2.0, bucketCount);
		var maxWeight = buckets.Max(b => b.Weight);
		return maxWeight > 0 ? buckets.Select(b => (float)(b.Weight / maxWeight)).ToArray() : new float[bucketCount];
	}

	private static IReadOnlyList<TxFoldVolumeBucket> BuildVolumeProfile(IReadOnlyList<Tick> ticks)
	{
		const int bucketCount = 50;
		return RangeStatsHelper
			.BucketSamples(ticks, t => (t.Bid + t.Ask) / 2.0, bucketCount)
			.Where(b => b.Weight > 0)
			.Select(b => new TxFoldVolumeBucket(b.Low, b.High, (int)b.Weight))
			.ToList();
	}

	private static int LowerBound(IReadOnlyList<Tick> ticks, DateTime from)
	{
		int lo = 0,
			hi = ticks.Count;
		while (lo < hi)
		{
			var mid = (lo + hi) / 2;
			if (ticks[mid].Timestamp < from)
				lo = mid + 1;
			else
				hi = mid;
		}
		return lo;
	}

	private static (CTraderBarRow Bar, double TrueHigh, double TrueLow)? BuildBar(
		IReadOnlyList<Tick> ticks,
		int startIndex,
		BarAggregationKind kind
	)
	{
		if (startIndex >= ticks.Count)
			return null;

		var openTime = ticks[startIndex].Timestamp;
		double open = 0,
			close = 0,
			prevMid = 0;
		var prices = new List<double>(ticks.Count - startIndex);
		int upVolume = 0,
			downVolume = 0;

		for (var i = startIndex; i < ticks.Count; i++)
		{
			var mid = (ticks[i].Bid + ticks[i].Ask) / 2.0;
			if (i == startIndex)
				open = mid;
			else if (mid > prevMid)
				upVolume++;
			else if (mid < prevMid)
				downVolume++;

			prevMid = mid;
			close = mid;
			prices.Add(mid);
		}

		prices.Sort();
		var mean = prices.Average();
		var trueHigh = prices[^1];
		var trueLow = prices[0];
		CTraderBarRow bar = kind switch
		{
			BarAggregationKind.Quartile
				=> new CTraderBarRow(openTime, Percentile(prices, 0.25), trueHigh, trueLow, Percentile(prices, 0.75), prices.Count),
			BarAggregationKind.Decile
				=> new CTraderBarRow(openTime, Percentile(prices, 0.1), trueHigh, trueLow, Percentile(prices, 0.9), prices.Count),
			BarAggregationKind.MeanStdDev
				=> new CTraderBarRow(openTime, mean, mean + StdDev(prices, mean), mean - StdDev(prices, mean), mean, prices.Count),
			_ => new CTraderBarRow(openTime, open, trueHigh, trueLow, close, prices.Count, upVolume, downVolume),
		};
		return (bar, trueHigh, trueLow);
	}

	// Linear interpolation between closest ranks -- matches Excel's PERCENTILE.INC and NumPy's default method.
	private static double Percentile(List<double> sortedValues, double fraction)
	{
		if (sortedValues.Count == 1)
			return sortedValues[0];

		var rank = fraction * (sortedValues.Count - 1);
		var lowerIndex = (int)Math.Floor(rank);
		var upperIndex = (int)Math.Ceiling(rank);
		if (lowerIndex == upperIndex)
			return sortedValues[lowerIndex];

		var weight = rank - lowerIndex;
		return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
	}

	private static double StdDev(List<double> values, double mean) =>
		values.Count <= 1 ? 0 : Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
}
