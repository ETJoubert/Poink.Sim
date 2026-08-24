using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models;

internal readonly record struct TimeProfileSummary(double PointOfControl, double ValueAreaHigh, double ValueAreaLow);

internal static class TimeProfileAnalysis
{
	private static (double[] Seconds, double[] UpSeconds, double[] DownSeconds)? RebinSecondsSplit(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		int bucketCount,
		out double totalSeconds,
		out double binMin,
		out double binSize
	)
	{
		totalSeconds = 0;
		binMin = 0;
		binSize = 0;
		if (buckets.Count == 0)
			return null;

		totalSeconds = buckets.Sum(b => b.Seconds);
		if (totalSeconds <= 0)
			return null;

		var seconds = new double[bucketCount];
		var upSeconds = new double[bucketCount];
		var downSeconds = new double[bucketCount];
		double min = buckets.Min(b => b.Price),
			max = buckets.Max(b => b.Price);
		binMin = min;
		if (max <= min)
		{
			int mid = bucketCount / 2;
			seconds[mid] = totalSeconds;
			upSeconds[mid] = buckets.Sum(b => b.UpSeconds);
			downSeconds[mid] = buckets.Sum(b => b.DownSeconds);
			return (seconds, upSeconds, downSeconds);
		}

		binSize = (max - min) / bucketCount;
		foreach (var bucket in buckets)
		{
			int idx = Math.Clamp((int)((bucket.Price - min) / binSize), 0, bucketCount - 1);
			seconds[idx] += bucket.Seconds;
			upSeconds[idx] += bucket.UpSeconds;
			downSeconds[idx] += bucket.DownSeconds;
		}
		return (seconds, upSeconds, downSeconds);
	}

	private static double[]? RebinSeconds(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		int bucketCount,
		out double totalSeconds,
		out double binMin,
		out double binSize
	) => RebinSecondsSplit(buckets, bucketCount, out totalSeconds, out binMin, out binSize)?.Seconds;

	public static float[] BuildRelativeHistogram(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		int bucketCount
	)
	{
		var histogram = new float[bucketCount];
		var seconds = RebinSeconds(buckets, bucketCount, out var totalSeconds, out _, out _);
		if (seconds is null)
			return histogram;

		for (int i = 0; i < bucketCount; i++)
			histogram[i] = (float)(seconds[i] / totalSeconds);
		return histogram;
	}

	public static (float[] Seconds, float[] Prices) BuildRawHistogram(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		int bucketCount
	)
	{
		var secondsOut = new float[bucketCount];
		var pricesOut = new float[bucketCount];
		var seconds = RebinSeconds(buckets, bucketCount, out _, out var binMin, out var binSize);
		if (seconds is null)
			return (secondsOut, pricesOut);

		for (int i = 0; i < bucketCount; i++)
		{
			secondsOut[i] = (float)seconds[i];
			pricesOut[i] = (float)(binMin + (i + 0.5) * binSize);
		}
		return (secondsOut, pricesOut);
	}

	public static (float[] Up, float[] Down, float[] Diff) BuildRelativeSplitHistogram(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		int bucketCount
	)
	{
		var up = new float[bucketCount];
		var down = new float[bucketCount];
		var diff = new float[bucketCount];
		if (RebinSecondsSplit(buckets, bucketCount, out var totalSeconds, out _, out _) is not { } split)
			return (up, down, diff);

		for (int i = 0; i < bucketCount; i++)
		{
			up[i] = (float)(split.UpSeconds[i] / totalSeconds);
			down[i] = (float)(split.DownSeconds[i] / totalSeconds);
			diff[i] = up[i] - down[i];
		}
		return (up, down, diff);
	}

	public static (float[] Up, float[] Down, float[] Diff, float[] Prices) BuildRawSplitHistogram(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		int bucketCount
	)
	{
		var up = new float[bucketCount];
		var down = new float[bucketCount];
		var diff = new float[bucketCount];
		var prices = new float[bucketCount];
		if (RebinSecondsSplit(buckets, bucketCount, out _, out var binMin, out var binSize) is not { } split)
			return (up, down, diff, prices);

		for (int i = 0; i < bucketCount; i++)
		{
			up[i] = (float)split.UpSeconds[i];
			down[i] = (float)split.DownSeconds[i];
			diff[i] = up[i] - down[i];
			prices[i] = (float)(binMin + (i + 0.5) * binSize);
		}
		return (up, down, diff, prices);
	}

	public static TimeProfileSummary? ComputeSummary(
		IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
		double bucketWidth,
		double valueAreaFraction = 0.70
	)
	{
		if (buckets.Count == 0)
			return null;

		var ordered = buckets.OrderBy(b => b.Price).ToList();
		double totalSeconds = ordered.Sum(b => b.Seconds);
		if (totalSeconds <= 0)
			return null;

		int pocIndex = 0;
		for (int i = 1; i < ordered.Count; i++)
			if (ordered[i].Seconds > ordered[pocIndex].Seconds)
				pocIndex = i;

		int lowIdx = pocIndex,
			highIdx = pocIndex;
		double includedSeconds = ordered[pocIndex].Seconds;
		double target = totalSeconds * valueAreaFraction;
		while (includedSeconds < target && (lowIdx > 0 || highIdx < ordered.Count - 1))
		{
			double belowSeconds = lowIdx > 0 ? ordered[lowIdx - 1].Seconds : -1;
			double aboveSeconds = highIdx < ordered.Count - 1 ? ordered[highIdx + 1].Seconds : -1;
			if (aboveSeconds >= belowSeconds)
			{
				highIdx++;
				includedSeconds += ordered[highIdx].Seconds;
			}
			else
			{
				lowIdx--;
				includedSeconds += ordered[lowIdx].Seconds;
			}
		}

		return new TimeProfileSummary(
			ordered[pocIndex].Price + bucketWidth / 2.0,
			ordered[highIdx].Price + bucketWidth,
			ordered[lowIdx].Price
		);
	}
}
