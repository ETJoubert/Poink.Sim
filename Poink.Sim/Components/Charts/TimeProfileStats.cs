using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Components.Charts;

// Point of Control and Value Area High/Low, time-weighted counterpart to VolumeProfileLevels.
internal readonly record struct TimeProfileLevels(double Poc, double ValueAreaHigh, double ValueAreaLow);

internal static class TimeProfileStats
{
	private const int BucketCount = 50;
	private const double ValueAreaFraction = 0.70;

	// Same "too little data to mean anything" floor as VolumeProfileStats, gated on sample count.
	private const int MinSamplesPerBucket = 3;

	public static TimeProfileLevels? Compute(IReadOnlyList<WeightedTimeSample> samples)
	{
		var totalCount = samples.Sum(s => s.Count);
		if (totalCount < BucketCount * MinSamplesPerBucket)
			return null;

		double min = samples.Min(s => s.Price),
			max = samples.Max(s => s.Price);
		if (max <= min)
			return new TimeProfileLevels(min, min, min);

		var seconds = new double[BucketCount];
		var size = (max - min) / BucketCount;
		foreach (var s in samples)
		{
			var idx = Math.Clamp((int)((s.Price - min) / size), 0, BucketCount - 1);
			seconds[idx] += s.Seconds;
		}

		var totalSeconds = seconds.Sum();
		// Enough samples but no elapsed time between them yet (e.g. right after Load/Reset).
		if (totalSeconds <= 0)
			return null;

		var pocIndex = 0;
		for (var i = 1; i < BucketCount; i++)
			if (seconds[i] > seconds[pocIndex])
				pocIndex = i;

		var lowIndex = pocIndex;
		var highIndex = pocIndex;
		var covered = seconds[pocIndex];
		var target = totalSeconds * ValueAreaFraction;
		while (covered < target && (lowIndex > 0 || highIndex < BucketCount - 1))
		{
			var belowSeconds = lowIndex > 0 ? seconds[lowIndex - 1] : -1;
			var aboveSeconds = highIndex < BucketCount - 1 ? seconds[highIndex + 1] : -1;

			if (aboveSeconds >= belowSeconds)
			{
				highIndex++;
				covered += seconds[highIndex];
			}
			else
			{
				lowIndex--;
				covered += seconds[lowIndex];
			}
		}

		return new TimeProfileLevels(
			min + size * (pocIndex + 0.5),
			min + size * (highIndex + 1),
			min + size * lowIndex
		);
	}
}
