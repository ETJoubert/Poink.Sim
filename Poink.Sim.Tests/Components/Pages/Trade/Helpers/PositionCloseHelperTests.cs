using System;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class PositionCloseHelperTests
{
	private const long Nasdaq = 1107;
	private const long Dow = 1287;

	private static readonly DateTime Opened = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);

	private static CTraderPositionInfo Position(long id, bool isLong, double entry, long symbolId = Nasdaq) =>
		new(id, symbolId, "X", isLong, 1, entry, Opened, null, null, 0);

	private static CloseAutomationSettings Automation(bool autoArm = false, bool autoClose = false) =>
		new(autoArm, ArmAfterSeconds: 30, autoClose, CloseAfterSeconds: 60);

	[Theory]
	[InlineData(true, 99.9, 100.1, false)]
	[InlineData(true, 100, 100.2, true)]
	[InlineData(true, 100.5, 100.7, true)]
	[InlineData(false, 99.8, 100.1, false)]
	[InlineData(false, 99.8, 100, true)]
	[InlineData(false, 99.3, 99.5, true)]
	public void IsAtOrPastEntry_MarksLongsOnTheBidAndShortsOnTheAsk(bool isLong, double bid, double ask, bool expected)
	{
		Assert.Equal(expected, PositionCloseHelper.IsAtOrPastEntry(Position(1, isLong, entry: 100), bid, ask));
	}

	[Theory]
	[InlineData(false, 100.5, PositionCloseReason.None)]
	[InlineData(true, 99.5, PositionCloseReason.None)]
	[InlineData(true, 100.5, PositionCloseReason.Breakeven)]
	public void Decide_ArmedSideClosesAtBreakevenWhateverItsAge(bool sideArmed, double bid, PositionCloseReason expected)
	{
		var reason = PositionCloseHelper.Decide(Position(1, true, 100), Opened, sideArmed, automation: null, bid, bid + 0.2);

		Assert.Equal(expected, reason);
	}

	[Theory]
	[InlineData(29, PositionCloseReason.None)]
	[InlineData(30, PositionCloseReason.Breakeven)]
	public void Decide_AutoArmWatchesAPositionOnlyOnceItIsTSecondsOld(int ageSeconds, PositionCloseReason expected)
	{
		var reason = PositionCloseHelper.Decide(
			Position(1, true, 100),
			Opened.AddSeconds(ageSeconds),
			sideArmed: false,
			Automation(autoArm: true),
			100.5,
			100.7
		);

		Assert.Equal(expected, reason);
	}

	[Theory]
	[InlineData(89, PositionCloseReason.None)]
	[InlineData(90, PositionCloseReason.TimeStop)]
	public void Decide_TimeStopClosesAtTPlusSWhateverThePnl(int ageSeconds, PositionCloseReason expected)
	{
		var reason = PositionCloseHelper.Decide(
			Position(1, true, 100),
			Opened.AddSeconds(ageSeconds),
			sideArmed: false,
			Automation(autoClose: true),
			95,
			95.2
		);

		Assert.Equal(expected, reason);
	}

	[Fact]
	public void Decide_TimeStopNeedsNoQuote()
	{
		var reason = PositionCloseHelper.Decide(Position(1, true, 100), Opened.AddSeconds(90), true, Automation(autoClose: true), null, null);

		Assert.Equal(PositionCloseReason.TimeStop, reason);
	}

	[Fact]
	public void Decide_WithoutAQuote_SkipsTheBreakevenCheck()
	{
		var reason = PositionCloseHelper.Decide(Position(1, true, 100), Opened, sideArmed: true, automation: null, null, null);

		Assert.Equal(PositionCloseReason.None, reason);
	}

	[Theory]
	[InlineData(-5, 0, 0, false)]
	[InlineData(15, 15, 0.5, false)]
	[InlineData(30, 30, 1, true)]
	[InlineData(90, 30, 1, true)]
	public void Countdown_ClampsTheElapsedTimeToTheDelay(int ageSeconds, double elapsed, double fraction, bool done)
	{
		var countdown = PositionCloseHelper.Countdown(Opened, Opened.AddSeconds(ageSeconds), totalSeconds: 30);

		Assert.Equal(elapsed, countdown.ElapsedSeconds, 9);
		Assert.Equal(fraction, countdown.Fraction, 9);
		Assert.Equal(30 - elapsed, countdown.RemainingSeconds, 9);
		Assert.Equal(done, countdown.IsDone);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(45, 45)]
	[InlineData(1_000_000, PositionCloseHelper.MaxSeconds)]
	public void ClampSeconds_KeepsDelaysWithinBounds(int seconds, int expected)
	{
		Assert.Equal(expected, PositionCloseHelper.ClampSeconds(seconds));
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

		var sides = PositionCloseHelper.SidesToRearm(entries, accountId: 7);

		Assert.Equal([new BreakevenSide(Nasdaq, true), new BreakevenSide(Dow, false)], sides);
	}

	[Fact]
	public void SidesToRearm_NothingStored_IsEmpty()
	{
		Assert.Empty(PositionCloseHelper.SidesToRearm(null, accountId: 7));
	}

	[Fact]
	public void ToEntries_TiesEachSideToTheAccount()
	{
		var entries = PositionCloseHelper.ToEntries(7, [new BreakevenSide(Nasdaq, false)]);

		Assert.Equal([new BreakevenArmedEntry(7, Nasdaq, false)], entries);
	}
}
