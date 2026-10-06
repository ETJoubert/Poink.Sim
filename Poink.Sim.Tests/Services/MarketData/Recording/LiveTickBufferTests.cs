using System.Linq;
using Poink.Sim.Services.MarketData.Recording;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Services.MarketData.Recording;

public class LiveTickBufferTests
{
	private const string Table = "live_ticks_eurusd";

	[Fact]
	public void Drain_SortsAndKeepsLatestQuotePerTimestamp()
	{
		var buffer = new LiveTickBuffer();
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(2, 1.2));
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(1, 1.1));
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(2, 1.3));

		var batch = buffer.Drain(Table);

		Assert.NotNull(batch);
		Assert.Equal(new[] { 1.1, 1.3 }, batch.Ticks.Select(t => t.Bid));
		Assert.Equal(0, buffer.Count);
	}

	[Fact]
	public void PendingTables_ReportsEarliestTickPerTable()
	{
		var buffer = new LiveTickBuffer();
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(5, 1.1));
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(3, 1.1));

		var pending = Assert.Single(buffer.PendingTables());

		Assert.Equal(TestTickHelper.Origin.AddSeconds(3), pending.EarliestUtc);
	}

	[Fact]
	public void DiscardBefore_KeepsNewerTicks()
	{
		var buffer = new LiveTickBuffer();
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(1, 1.1));
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(5, 1.2));

		Assert.Equal(1, buffer.DiscardBefore(Table, TestTickHelper.Origin.AddSeconds(3)));
		Assert.Equal(new[] { 1.2 }, buffer.Drain(Table)!.Ticks.Select(t => t.Bid));
	}

	[Fact]
	public void Discard_DropsTableAndReturnsCount()
	{
		var buffer = new LiveTickBuffer();
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(1, 1.1));
		buffer.Add(Table, 1, "EURUSD", TestTickHelper.AtMid(2, 1.1));

		Assert.Equal(2, buffer.Discard(Table));
		Assert.Null(buffer.Drain(Table));
	}
}
