using System.Linq;
using Poink.Sim.Models;
using Xunit;

namespace Poink.Sim.Tests.Models;

public class BarPriceHistogramTests
{
	[Fact]
	public void Add_BooksDirectionAndDistanceToOriginBucket()
	{
		var histogram = new BarPriceHistogram(0.5);

		histogram.Add(10.2, 1, 0.3);
		histogram.Add(10.4, -1, 0.1);
		histogram.Add(10.6, 0, 0);

		var lower = histogram.Buckets[20];
		Assert.Equal(2, lower.Count);
		Assert.Equal(1, lower.UpCount);
		Assert.Equal(1, lower.DownCount);
		Assert.Equal(0.3, lower.UpDistance, 9);
		Assert.Equal(10.2, lower.UpPriceSum, 9);
		Assert.Equal(10.4, lower.DownPriceSum, 9);
		Assert.Equal(1, histogram.Buckets[21].Count);
	}

	[Fact]
	public void Merge_SumsOnlyTheTrailingWindow()
	{
		var oldest = new BarPriceHistogram(1.0);
		oldest.Add(5.5, 1, 1);
		var middle = new BarPriceHistogram(1.0);
		middle.Add(10.5, 1, 1);
		var newest = new BarPriceHistogram(1.0);
		newest.Add(10.2, -1, 1);
		newest.Add(11.2, 1, 1);

		var merged = BarPriceHistogram.Merge([oldest, middle, newest], 2).OrderBy(bucket => bucket.Price).ToList();

		Assert.Equal(2, merged.Count);
		Assert.Equal((10.0, 2, 1, 1), merged[0]);
		Assert.Equal((11.0, 1, 1, 0), merged[1]);
	}
}
