using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class RangeStatsHelperTests
{
	private static readonly (double Price, double Weight)[] Weighted = [(1, 1), (2, 1), (3, 1), (4, 5)];

	[Fact]
	public void Compute_Empty_ReturnsNull()
	{
		Assert.Null(RangeStatsHelper.Compute(new List<double>(), price => price));
	}

	[Fact]
	public void Compute_Weighted_SeparatesVwapFromPlainAverage()
	{
		var stats = RangeStatsHelper.Compute(Weighted, item => item.Price, item => item.Weight)!.Value;

		Assert.Equal(3.25, stats.Vwap, 9);
		Assert.Equal(2.5, stats.Average, 9);
		Assert.Equal(4, stats.Median);
		Assert.Equal(4, stats.Regression, 9);
		Assert.Equal(4, stats.Max);
		Assert.Equal(1, stats.Min);
		Assert.Equal(4, stats.Count);
	}

	[Fact]
	public void Compute_Unweighted_VwapEqualsAverage()
	{
		var stats = RangeStatsHelper.Compute(Weighted, item => item.Price)!.Value;

		Assert.Equal(stats.Average, stats.Vwap, 9);
		Assert.Equal(2, stats.Median);
	}

	[Fact]
	public void Compute_TooFewSamples_LeavesVolumeProfileEmpty()
	{
		var stats = RangeStatsHelper.Compute(Weighted, item => item.Price)!.Value;

		Assert.Null(stats.Poc);
		Assert.Null(stats.Vah);
		Assert.Null(stats.Val);
	}

	[Fact]
	public void Compute_EnoughSamples_PlacesPocAtTheHeaviestPriceInsideValueArea()
	{
		var prices = Enumerable.Range(0, 100).Select(i => (double)i).Concat(Enumerable.Repeat(50.5, 100)).ToList();

		var stats = RangeStatsHelper.Compute(prices, price => price)!.Value;

		Assert.InRange(stats.Poc!.Value, 49.5, 51.5);
		Assert.True(stats.Val <= stats.Poc && stats.Poc <= stats.Vah);
	}

	[Fact]
	public void Compute_SinglePriceWithEnoughSamples_CollapsesProfileToThatPrice()
	{
		var stats = RangeStatsHelper.Compute(Enumerable.Repeat(7.0, 200).ToList(), price => price)!.Value;

		Assert.Equal(7, stats.Poc);
		Assert.Equal(7, stats.Vah);
		Assert.Equal(7, stats.Val);
	}

	[Fact]
	public void WidenValueArea_GrowsTowardsHeavierNeighbour()
	{
		Assert.Equal((2, 1, 2), RangeStatsHelper.WidenValueArea([1, 5, 10, 3, 1], 15));
	}

	[Fact]
	public void WidenValueArea_TiedNeighbours_PreferHigherSide()
	{
		Assert.Equal((1, 1, 2), RangeStatsHelper.WidenValueArea([2, 10, 2], 12));
	}

	[Fact]
	public void WidenValueArea_UnreachableTarget_StopsAtBothEdges()
	{
		Assert.Equal((1, 0, 2), RangeStatsHelper.WidenValueArea([1, 2, 1], 100));
	}

	[Fact]
	public void BucketSamples_SplitsRangeIntoEqualWidthBuckets()
	{
		var buckets = RangeStatsHelper.BucketSamples(new[] { 0.0, 1, 2, 3, 4 }, price => price, 5);

		Assert.Equal(5, buckets.Count);
		Assert.All(buckets, bucket => Assert.Equal(1, bucket.Weight));
		Assert.Equal(0, buckets[0].Low, 9);
		Assert.Equal(0.8, buckets[0].High, 9);
		Assert.Equal(4, buckets[^1].High, 9);
	}

	[Fact]
	public void BucketSamples_SinglePrice_PutsAllWeightInFirstBucket()
	{
		var buckets = RangeStatsHelper.BucketSamples(new[] { 3.0, 3.0, 3.0 }, price => price, 4);

		Assert.Equal(3, buckets[0].Weight);
		Assert.All(buckets.Skip(1), bucket => Assert.Equal(0, bucket.Weight));
	}

	[Fact]
	public void BucketSamples_NoBuckets_ReturnsEmpty()
	{
		Assert.Empty(RangeStatsHelper.BucketSamples(new[] { 1.0 }, price => price, 0));
	}

	[Fact]
	public void ComputeValueArea_OrdersBucketsAndReportsBucketEdges()
	{
		(double Price, double Weight)[] buckets = [(13, 1), (11, 6), (10, 1), (12, 2)];

		var area = RangeStatsHelper.ComputeValueArea(buckets, bucket => bucket.Price, bucket => bucket.Weight, 1.0)!.Value;

		Assert.Equal(11.5, area.Poc);
		Assert.Equal(13, area.Vah);
		Assert.Equal(11, area.Val);
	}

	[Fact]
	public void ComputeValueArea_NoWeight_ReturnsNull()
	{
		(double Price, double Weight)[] buckets = [(10, 0), (11, 0)];

		Assert.Null(RangeStatsHelper.ComputeValueArea(buckets, bucket => bucket.Price, bucket => bucket.Weight, 1.0));
	}

	[Fact]
	public void LinearFit_FitsLineAgainstIndex()
	{
		var (slope, intercept) = RangeStatsHelper.LinearFit([1, 3, 5]);

		Assert.Equal(2, slope, 9);
		Assert.Equal(1, intercept, 9);
	}

	[Fact]
	public void LinearFit_SinglePoint_IsFlatAtThatValue()
	{
		Assert.Equal((0.0, 4.0), RangeStatsHelper.LinearFit([4]));
	}
}
