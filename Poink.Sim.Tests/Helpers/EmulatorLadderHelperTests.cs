using System.Linq;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class EmulatorLadderHelperTests
{
	private static readonly OrderBook Book = new(
		[new OrderBookLevel(9, 5), new OrderBookLevel(8, 7)],
		[new OrderBookLevel(11, 3), new OrderBookLevel(12, 4), new OrderBookLevel(13, 6)]
	);

	private static readonly PopulationLevel[] Population = [new(11, 2, 400), new(9, 1, 500), new(9, 2, 400)];

	private static readonly EmulatorMarketScale OneTickRows = new(7, 13, 1, 1);

	[Fact]
	public void BuildRows_RunsFromHighestPriceDownOneRowPerScaleRow()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, OneTickRows);

		Assert.Equal([13.0, 12, 11, 10, 9, 8, 7], rows.Select(row => row.Price));
	}

	[Fact]
	public void BuildRows_MarksTheLastPricesRow()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 12, OneTickRows);

		Assert.Equal(1, rows.ToList().FindIndex(row => row.IsCurrentPrice));
		Assert.Single(rows, row => row.IsCurrentPrice);
	}

	[Fact]
	public void BuildRows_PlacesAsksAboveAndBidsBelowThePrice()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, OneTickRows);

		Assert.Equal([6.0, 4, 3, null, null, null, null], rows.Select(row => row.AskVolume));
		Assert.Equal([null, null, null, null, 5.0, 7, null], rows.Select(row => row.BidVolume));
	}

	[Fact]
	public void BuildRows_PutsEachLevelsBotsOnItsRow()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, OneTickRows);

		Assert.Equal([null, null, 2, null, 3, null, null], rows.Select(row => row.Population?.BotCount));
		Assert.Equal(900, rows[4].Population!.Value.Balance);
	}

	[Fact]
	public void BuildRows_GroupedRowsAddUpTheirTicks()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, new EmulatorMarketScale(6, 13, 2, 1));

		Assert.Equal([(12.0, 13.0), (10, 11), (8, 9), (6, 7)], rows.Select(row => (row.Price, row.TopPrice)));
		Assert.Equal([10.0, 3, null, null], rows.Select(row => row.AskVolume));
		Assert.Equal([null, null, 12.0, null], rows.Select(row => row.BidVolume));
		Assert.True(rows[1].IsCurrentPrice);
	}

	[Fact]
	public void BuildRows_LeavesOutLevelsBeyondTheScale()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, new EmulatorMarketScale(9, 11, 1, 1));

		Assert.Equal([3.0, null, null], rows.Select(row => row.AskVolume));
		Assert.Equal([null, null, 5.0], rows.Select(row => row.BidVolume));
	}
}
