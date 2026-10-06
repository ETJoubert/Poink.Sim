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

	[Fact]
	public void BuildRows_PutsCurrentPriceInTheMiddleRow()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, 1);

		Assert.Equal(7, rows.Count);
		Assert.True(rows[3].IsCurrentPrice);
		Assert.Equal(10, rows[3].Price);
		Assert.Single(rows, row => row.IsCurrentPrice);
	}

	[Fact]
	public void BuildRows_RunsFromHighestPriceDownOneTickPerRow()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, 1);

		Assert.Equal([13.0, 12, 11, 10, 9, 8, 7], rows.Select(row => row.Price));
	}

	[Fact]
	public void BuildRows_PlacesAsksAboveAndBidsBelowThePrice()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, 1);

		Assert.Equal([6.0, 4, 3, null, null, null, null], rows.Select(row => row.AskVolume));
		Assert.Equal([null, null, null, null, 5.0, 7, null], rows.Select(row => row.BidVolume));
	}

	[Fact]
	public void BuildRows_PutsEachLevelsBotsOnItsRow()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, Population, 10, 1);

		Assert.Equal([null, null, 2, null, 3, null, null], rows.Select(row => row.Population?.BotCount));
		Assert.Equal(900, rows[4].Population!.Value.Balance);
	}

	[Fact]
	public void BuildRows_WidensToFitBotsBeyondTheBook()
	{
		var rows = EmulatorLadderHelper.BuildRows(Book, [new PopulationLevel(5, 1, 50)], 10, 1);

		Assert.Equal(11, rows.Count);
		Assert.Equal(5, rows[^1].Price);
		Assert.Equal(1, rows[^1].Population!.Value.BotCount);
	}

	[Fact]
	public void BuildRows_EmptyBookIsJustTheCurrentPrice()
	{
		var row = Assert.Single(EmulatorLadderHelper.BuildRows(OrderBook.Empty, [], 100.004, 0.01));

		Assert.Equal(100, row.Price);
		Assert.True(row.IsCurrentPrice);
	}
}
