using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class SplitTickReadHelperTests
{
	private const string Live = "live_ticks_eurusd";
	private const string Demo = "demo_ticks_eurusd";

	private readonly InMemoryTickRepository _store = new();

	public SplitTickReadHelperTests()
	{
		_store.Seed(Live, [TestTickHelper.AtMid(10, 1), TestTickHelper.AtMid(20, 2), TestTickHelper.AtMid(30, 3)]);
		_store.Seed(Demo, [TestTickHelper.AtMid(15, 10), TestTickHelper.AtMid(25, 20), TestTickHelper.AtMid(35, 30)]);
	}

	[Fact]
	public void Read_WithoutHistoryTable_ReadsOnlyMainTable()
	{
		var ticks = SplitTickReadHelper.Read(_store, Demo, null, TestTickHelper.Origin.AddSeconds(22), TestTickHelper.Origin, TestTickHelper.Origin.AddSeconds(40));

		Assert.Equal(new[] { 10.0, 20, 30 }, ticks.Select(t => t.Bid));
	}

	[Fact]
	public void Read_Split_TakesHistoryBeforeBoundaryAndMainTableAfter()
	{
		var ticks = SplitTickReadHelper.Read(_store, Demo, Live, TestTickHelper.Origin.AddSeconds(22), TestTickHelper.Origin, TestTickHelper.Origin.AddSeconds(40));

		Assert.Equal(new[] { 1.0, 2, 20, 30 }, ticks.Select(t => t.Bid));
	}

	[Fact]
	public void Read_RangeEntirelyAfterBoundary_ReadsOnlyMainTable()
	{
		var ticks = SplitTickReadHelper.Read(_store, Demo, Live, TestTickHelper.Origin, TestTickHelper.Origin.AddSeconds(12), TestTickHelper.Origin.AddSeconds(40));

		Assert.Equal(new[] { 10.0, 20, 30 }, ticks.Select(t => t.Bid));
	}

	[Fact]
	public void Read_SameTables_IsASingleRead()
	{
		var ticks = SplitTickReadHelper.Read(_store, Live, Live, TestTickHelper.Origin.AddSeconds(22), TestTickHelper.Origin, TestTickHelper.Origin.AddSeconds(40));

		Assert.Equal(3, ticks.Count);
	}
}
