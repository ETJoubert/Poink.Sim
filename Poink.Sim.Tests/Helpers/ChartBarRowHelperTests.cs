using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class ChartBarRowHelperTests
{
	[Fact]
	public void ToQuartileBarRows_MapsBoxPlotOntoCandle()
	{
		var bar = new QuartileBar(TestTickHelper.Origin, TestTickHelper.Origin.AddMinutes(1), 1, 2, 3, 4, 5, 9);

		var row = Assert.Single(ChartBarRowHelper.ToQuartileBarRows([bar]));

		Assert.Equal((2.0, 5.0, 1.0, 4.0, 9L), (row.Open, row.High, row.Low, row.Close, row.Volume));
		Assert.Equal(3, Assert.Single(ChartBarRowHelper.ToMedianLine([bar])).Price);
	}

	[Fact]
	public void ToBarRows_CarriesFlowColumns()
	{
		var bar = TestBarHelper.Bar(12, 9, 11, upVolume: 4, downVolume: 2);

		var row = Assert.Single(ChartBarRowHelper.ToBarRows([bar]));

		Assert.Equal((12.0, 9.0, 11.0, 6L, 4L, 2L), (row.High, row.Low, row.Close, row.Volume, row.UpVolume!.Value, row.DownVolume!.Value));
	}
}
