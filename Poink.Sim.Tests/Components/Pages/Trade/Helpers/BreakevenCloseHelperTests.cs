using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class BreakevenCloseHelperTests
{
	private const long Nasdaq = 1107;
	private const long Dow = 1287;

	private static CTraderPositionInfo Position(long id, bool isLong, double entry, long symbolId = Nasdaq) =>
		new(id, symbolId, "X", isLong, 1, entry, DateTime.UtcNow, null, null, 0);

	[Theory]
	[InlineData(true, 99.9, 100.1, false)]
	[InlineData(true, 100, 100.2, true)]
	[InlineData(true, 100.5, 100.7, true)]
	[InlineData(false, 99.8, 100.1, false)]
	[InlineData(false, 99.8, 100, true)]
	[InlineData(false, 99.3, 99.5, true)]
	public void IsAtOrPastEntry_MarksLongsOnTheBidAndShortsOnTheAsk(bool isLong, double bid, double ask, bool expected)
	{
		Assert.Equal(expected, BreakevenCloseHelper.IsAtOrPastEntry(Position(1, isLong, entry: 100), bid, ask));
	}

	[Fact]
	public void SidesToRearm_KeepsOnlyTheCurrentAccountsSidesOnce()
	{
		var entries = new[]
		{
			new BreakevenArmedEntry(7, Nasdaq, true),
			new BreakevenArmedEntry(8, Nasdaq, false),
			new BreakevenArmedEntry(7, Dow, false),
			new BreakevenArmedEntry(7, Nasdaq, true),
		};

		var sides = BreakevenCloseHelper.SidesToRearm(entries, accountId: 7);

		Assert.Equal([new BreakevenSide(Nasdaq, true), new BreakevenSide(Dow, false)], sides);
	}

	[Fact]
	public void SidesToRearm_NothingStored_IsEmpty()
	{
		Assert.Empty(BreakevenCloseHelper.SidesToRearm(null, accountId: 7));
	}

	[Fact]
	public void ToEntries_TiesEachSideToTheAccount()
	{
		var entries = BreakevenCloseHelper.ToEntries(7, [new BreakevenSide(Nasdaq, false)]);

		Assert.Equal([new BreakevenArmedEntry(7, Nasdaq, false)], entries);
	}

	[Fact]
	public void DuePositions_TakesOnlyTheSidesPositionsAtBreakevenThatAreNotAlreadyClosing()
	{
		var positions = new[]
		{
			Position(1, isLong: true, entry: 100),
			Position(2, isLong: true, entry: 101),
			Position(3, isLong: true, entry: 99),
			Position(4, isLong: false, entry: 102),
			Position(5, isLong: true, entry: 90, symbolId: Dow),
		};

		var due = BreakevenCloseHelper.DuePositions(
			positions,
			new BreakevenSide(Nasdaq, IsLong: true),
			bid: 100.5,
			ask: 100.7,
			closingPositionIds: new HashSet<long> { 3 }
		);

		Assert.Equal([1L], due.Select(p => p.PositionId));
	}
}
