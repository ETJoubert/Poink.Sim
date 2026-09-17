using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.Charts;

internal interface ITimeframeSnapshotService
{
	Task<List<(int LookbackSeconds, CTraderBarRow Bar)>> BuildSnapshotBarsAsync(
		string table,
		DateTime anchorEnd,
		IReadOnlyCollection<int> lookbackSeconds,
		BarAggregationKind kind
	);
}

// Windows overlap by design (each ends at anchorEnd), so BarKindChartService's fixed-epoch
// bucketing can't be reused -- it would silently split an unaligned range across two buckets.
internal sealed class TimeframeSnapshotService : ITimeframeSnapshotService
{
	private readonly ITickRepository _tickRepository;

	public TimeframeSnapshotService(ITickRepository tickRepository) => _tickRepository = tickRepository;

	public Task<List<(int LookbackSeconds, CTraderBarRow Bar)>> BuildSnapshotBarsAsync(
		string table,
		DateTime anchorEnd,
		IReadOnlyCollection<int> lookbackSeconds,
		BarAggregationKind kind
	) =>
		Task.Run(() =>
			lookbackSeconds
				.Distinct()
				.OrderBy(seconds => seconds)
				.Select(seconds => (Seconds: seconds, Bar: BuildBar(table, anchorEnd.AddSeconds(-seconds), anchorEnd, kind)))
				.Where(pair => pair.Bar is not null)
				.Select(pair => (pair.Seconds, pair.Bar!))
				.ToList()
		);

	private CTraderBarRow? BuildBar(string table, DateTime from, DateTime to, BarAggregationKind kind)
	{
		DateTime? openTime = null;
		double open = 0,
			close = 0,
			prevMid = 0;
		var prices = new List<double>();
		int upVolume = 0,
			downVolume = 0;

		foreach (var tick in _tickRepository.ReadTicks(table, from, to))
		{
			var mid = (tick.Bid + tick.Ask) / 2.0;
			if (openTime is null)
				open = mid;
			else if (mid > prevMid)
				upVolume++;
			else if (mid < prevMid)
				downVolume++;

			openTime ??= tick.Timestamp;
			prevMid = mid;
			close = mid;
			prices.Add(mid);
		}

		if (openTime is null)
			return null;

		prices.Sort();
		return kind == BarAggregationKind.Quartile
			? new CTraderBarRow(openTime.Value, Percentile(prices, 0.25), prices[^1], prices[0], Percentile(prices, 0.75), prices.Count)
			: new CTraderBarRow(openTime.Value, open, prices[^1], prices[0], close, prices.Count, upVolume, downVolume);
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
}
