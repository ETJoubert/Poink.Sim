using System.Collections.Generic;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Charts;

// Point of Control (most volume) and Value Area High/Low (tightest band holding ValueAreaFraction of total volume).
internal readonly record struct VolumeProfileLevels(double Poc, double ValueAreaHigh, double ValueAreaLow);

internal static class VolumeProfileStats
{
	public static VolumeProfileLevels? Compute(IReadOnlyList<WeightedPriceSample> samples) =>
		Stats(samples) is { Poc: { } poc, Vah: { } vah, Val: { } val } ? new VolumeProfileLevels(poc, vah, val) : null;

	public static double? ComputeAverage(IReadOnlyList<WeightedPriceSample> samples) => Stats(samples)?.Average;

	public static double? ComputeMedian(IReadOnlyList<WeightedPriceSample> samples) => Stats(samples)?.Median;

	private static RangeStatsResult? Stats(IReadOnlyList<WeightedPriceSample> samples) =>
		RangeStatsHelper.Compute(samples, s => s.Price, s => (double)s.Count);
}
