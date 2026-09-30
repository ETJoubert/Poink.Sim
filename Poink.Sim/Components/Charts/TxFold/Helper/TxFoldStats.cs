using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Charts;

internal readonly record struct TxFoldVolumeBucket(double Low, double High, int Count);

internal readonly record struct TxFoldBar(int LookbackSeconds, CTraderBarRow Bar, bool HasFullCoverage, double TrueHigh, double TrueLow);

/// <summary>Levels is null only when there are no ticks at all -- unlike Poc/Vah/Val inside it,
/// which can independently be null just from too little data for a meaningful value area.</summary>
internal readonly record struct TxFoldSnapshot(List<TxFoldBar> Bars, IReadOnlyList<TxFoldVolumeBucket> VolumeProfile, RangeStatsResult? Levels);

// Bar/coverage/volume-profile math over already-fetched ticks, kept separate from TxFold.razor.cs.
internal static class TxFoldStats
{
	// Windows overlap by design (each ends at anchorEnd), so BarKindChartService's fixed-epoch bucketing can't be reused.
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
			if (BarAggregator.Aggregate(ticks, startIndex, kind) is { } result)
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
}
