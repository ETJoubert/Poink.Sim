using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Simulation;

public class WatchedSymbolFeedTests
{
	private static readonly List<Tick> Ticks =
	[
		TestTickHelper.AtQuote(0, 10, 11),
		TestTickHelper.AtQuote(20, 12, 13),
		TestTickHelper.AtQuote(40, 9, 10),
		TestTickHelper.AtQuote(65, 15, 16),
	];

	private static WatchedSymbolFeed Started(bool aggregate = true, int barSeconds = 60)
	{
		var feed = new WatchedSymbolFeed(new SimulationWatchSymbol("ticks_test", 1.0), () => Ticks.GetEnumerator());
		feed.Restart(aggregate, TimeSpan.FromSeconds(barSeconds));
		return feed;
	}

	[Fact]
	public void Snapshot_BeforeAnyAdvance_HasNoQuote()
	{
		var snapshot = Started().Snapshot();

		Assert.Equal("ticks_test", snapshot.Table);
		Assert.Null(snapshot.CurrentBid);
		Assert.Null(snapshot.CurrentPrice);
		Assert.Empty(snapshot.RecentTicks);
		Assert.Empty(snapshot.RecentBars);
	}

	[Fact]
	public void AdvanceTo_BeforeFirstTick_ConsumesNothing()
	{
		var feed = Started();

		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(-1));

		Assert.Empty(feed.Snapshot().RecentTicks);
	}

	[Fact]
	public void AdvanceTo_ConsumesTicksAtOrBeforeTimestampOnly()
	{
		var feed = Started();

		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(20));

		var snapshot = feed.Snapshot();
		Assert.Equal(2, snapshot.RecentTicks.Count);
		Assert.Equal(12, snapshot.CurrentBid);
		Assert.Equal(13, snapshot.CurrentAsk);
		Assert.Equal(12.5, snapshot.CurrentPrice);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(20), snapshot.CurrentTimestampUtc);
	}

	[Fact]
	public void AdvanceTo_HeldTickIsConsumedByALaterAdvance()
	{
		var feed = Started();

		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(30));
		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(45));

		var snapshot = feed.Snapshot();
		Assert.Equal(3, snapshot.RecentTicks.Count);
		Assert.Equal(9, snapshot.CurrentBid);
	}

	[Fact]
	public void AdvanceTo_PastLastTick_KeepsLastQuote()
	{
		var feed = Started();

		feed.AdvanceTo(TestTickHelper.Origin.AddHours(1));
		feed.AdvanceTo(TestTickHelper.Origin.AddHours(2));

		var snapshot = feed.Snapshot();
		Assert.Equal(4, snapshot.RecentTicks.Count);
		Assert.Equal(15, snapshot.CurrentBid);
	}

	[Fact]
	public void Snapshot_Aggregating_HasCompletedBarsAndTheFormingBar()
	{
		var feed = Started();

		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(65));

		var snapshot = feed.Snapshot();
		var bars = snapshot.RecentBars;
		Assert.Equal(2, bars.Count);
		Assert.Equal(2, snapshot.RecentBarHistograms.Count);
		Assert.Equal(TestTickHelper.Origin, bars[0].OpenTime);
		Assert.Equal(10.5, bars[0].Open);
		Assert.Equal(12.5, bars[0].High);
		Assert.Equal(9.5, bars[0].Low);
		Assert.Equal(9.5, bars[0].Close);
		Assert.Equal(TestTickHelper.Origin.AddMinutes(1), bars[1].OpenTime);
		Assert.Equal(15.5, bars[1].Close);
	}

	[Fact]
	public void Snapshot_NotAggregating_HasTicksButNoBars()
	{
		var feed = Started(aggregate: false);

		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(65));

		var snapshot = feed.Snapshot();
		Assert.Empty(snapshot.RecentBars);
		Assert.Equal(4, snapshot.RecentTicks.Count);
	}

	[Fact]
	public void Reaggregate_RebuildsBarsFromReplayedTicksAtNewSize()
	{
		var feed = Started(aggregate: true, barSeconds: 60);
		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(65));

		feed.Reaggregate(true, TimeSpan.FromSeconds(30));

		var bars = feed.Snapshot().RecentBars;
		Assert.Equal(3, bars.Count);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(30), bars[1].OpenTime);
		Assert.Equal(4, feed.Snapshot().RecentTicks.Count);
	}

	[Fact]
	public void Restart_RewindsToTheFirstTick()
	{
		var feed = Started();
		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(65));

		feed.Restart(true, TimeSpan.FromMinutes(1));
		feed.AdvanceTo(TestTickHelper.Origin);

		var snapshot = feed.Snapshot();
		Assert.Single(snapshot.RecentTicks);
		Assert.Equal(10, snapshot.CurrentBid);
		Assert.Single(snapshot.RecentBars);
	}

	[Fact]
	public void Snapshot_WithoutNewTicks_ReusesThePublishedTickList()
	{
		var feed = Started();
		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(20));

		var first = feed.Snapshot().RecentTicks;
		feed.AdvanceTo(TestTickHelper.Origin.AddSeconds(30));

		Assert.Same(first, feed.Snapshot().RecentTicks);
	}
}
