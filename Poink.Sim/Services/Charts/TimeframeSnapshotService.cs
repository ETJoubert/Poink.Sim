using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.Charts;

internal interface ITimeframeSnapshotService
{
	Task<SnapshotResult> BuildSnapshotBarsAsync(
		string table,
		DateTime anchorEnd,
		IReadOnlyCollection<int> lookbackSeconds,
		BarAggregationKind kind
	);
}

internal readonly record struct VolumeProfileBucket(double Low, double High, int Count);

internal readonly record struct SnapshotResult(
	List<(int LookbackSeconds, CTraderBarRow Bar)> Bars,
	IReadOnlyList<VolumeProfileBucket> VolumeProfile
);

// Windows overlap by design (each ends at anchorEnd), so BarKindChartService's fixed-epoch
// bucketing can't be reused -- it would silently split an unaligned range across two buckets.
internal sealed class TimeframeSnapshotService : ITimeframeSnapshotService
{
	private readonly ITickRepository _tickRepository;

	public TimeframeSnapshotService(ITickRepository tickRepository) => _tickRepository = tickRepository;

	public Task<SnapshotResult> BuildSnapshotBarsAsync(
		string table,
		DateTime anchorEnd,
		IReadOnlyCollection<int> lookbackSeconds,
		BarAggregationKind kind
	) =>
		Task.Run(() =>
		{
			var distinctSeconds = lookbackSeconds.Distinct().OrderBy(seconds => seconds).ToList();
			if (distinctSeconds.Count == 0)
				return new SnapshotResult(new List<(int, CTraderBarRow)>(), Array.Empty<VolumeProfileBucket>());

			// One scan over the largest window, not one per timeframe -- every smaller window is a
			// suffix of this same tick list (sliced below), and the volume profile shares it too.
			var ticks = _tickRepository.ReadTicks(table, anchorEnd.AddSeconds(-distinctSeconds[^1]), anchorEnd).ToList();

			var bars = new List<(int, CTraderBarRow)>(distinctSeconds.Count);
			foreach (var seconds in distinctSeconds)
				if (BuildBar(ticks, LowerBound(ticks, anchorEnd.AddSeconds(-seconds)), kind) is { } bar)
					bars.Add((seconds, bar));

			return new SnapshotResult(bars, BuildVolumeProfile(ticks));
		});

	// Same uniform-bucket tick-count histogram VolumeProfileStats.Compute uses elsewhere, over the
	// full tick list rather than a Lookback Window slice of it.
	private static IReadOnlyList<VolumeProfileBucket> BuildVolumeProfile(List<Tick> ticks)
	{
		const int bucketCount = 50;
		if (ticks.Count == 0)
			return Array.Empty<VolumeProfileBucket>();

		var prices = ticks.Select(t => (t.Bid + t.Ask) / 2.0).ToList();
		var min = prices.Min();
		var max = prices.Max();
		if (max <= min)
			return new[] { new VolumeProfileBucket(min, min, prices.Count) };

		var counts = new int[bucketCount];
		var size = (max - min) / bucketCount;
		foreach (var price in prices)
			counts[Math.Clamp((int)((price - min) / size), 0, bucketCount - 1)]++;

		return Enumerable
			.Range(0, bucketCount)
			.Where(i => counts[i] > 0)
			.Select(i => new VolumeProfileBucket(min + size * i, min + size * (i + 1), counts[i]))
			.ToList();
	}

	// First index whose tick timestamp is >= from, via binary search over the timestamp-sorted list.
	private static int LowerBound(List<Tick> ticks, DateTime from)
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

	private static CTraderBarRow? BuildBar(List<Tick> ticks, int startIndex, BarAggregationKind kind)
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
		return kind switch
		{
			BarAggregationKind.Quartile
				=> new CTraderBarRow(openTime, Percentile(prices, 0.25), prices[^1], prices[0], Percentile(prices, 0.75), prices.Count),
			BarAggregationKind.Decile
				=> new CTraderBarRow(openTime, Percentile(prices, 0.1), prices[^1], prices[0], Percentile(prices, 0.9), prices.Count),
			BarAggregationKind.MeanStdDev
				=> new CTraderBarRow(openTime, mean, mean + StdDev(prices, mean), mean - StdDev(prices, mean), mean, prices.Count),
			_ => new CTraderBarRow(openTime, open, prices[^1], prices[0], close, prices.Count, upVolume, downVolume),
		};
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
