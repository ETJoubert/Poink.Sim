using System;
using System.Linq;
using Poink.Sim.Components.Pages.Trade;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Services;

public class TradeSymbolSeriesTests
{
	private static TickHistoryResult History(params Tick[] ticks) =>
		new(ticks, "live_ticks_eurusd", "live_ticks_eurusd", ticks.Length, 0, false, Array.Empty<DateOnly>());

	private static TradeSymbolSeries Series(params Tick[] ticks) => TradeSymbolSeries.FromHistory(1, "EURUSD", 60, 0.0001, History(ticks));

	[Fact]
	public void FromHistory_AggregatesCompletedBarsPlusTheFormingOne()
	{
		var series = Series(TestTickHelper.AtMid(5, 1.1), TestTickHelper.AtMid(65, 1.2), TestTickHelper.AtMid(70, 1.3));

		Assert.Equal(new[] { 1.1, 1.3 }, series.HlocBars.Select(b => b.Close));
		Assert.Equal(2, series.Bars.Count);
		Assert.Equal(2, series.Histograms.Count);
	}

	[Fact]
	public void Apply_WithinFormingBar_UpdatesItInPlace()
	{
		var series = Series(TestTickHelper.AtMid(5, 1.1));

		series.Apply(TestTickHelper.AtMid(30, 1.4));

		var bar = Assert.Single(series.HlocBars);
		Assert.Equal((1.4, 1.4), (bar.Close, bar.High));
	}

	[Fact]
	public void Apply_CrossingBoundary_FinalizesBarAndStartsNext()
	{
		var series = Series(TestTickHelper.AtMid(5, 1.1));

		series.Apply(TestTickHelper.AtMid(61, 1.2));

		Assert.Equal(new[] { 1.1, 1.2 }, series.HlocBars.Select(b => b.Close));
		Assert.Equal(TestTickHelper.Origin.AddMinutes(1), series.Bars[^1].TimeUtc);
	}

	[Fact]
	public void Apply_PublishesNewListsSoHeldReferencesStayUnchanged()
	{
		var series = Series(TestTickHelper.AtMid(5, 1.1));
		var before = series.Bars;

		series.Apply(TestTickHelper.AtMid(30, 1.4));

		Assert.NotSame(before, series.Bars);
		Assert.Equal(1.1, before[0].Close);
	}

	[Fact]
	public void TicksBetween_CoversHistoryAndLiveTicks()
	{
		var series = Series(TestTickHelper.AtMid(5, 1.1), TestTickHelper.AtMid(20, 1.2));
		series.Apply(TestTickHelper.AtMid(30, 1.3));

		Assert.Equal(new[] { 1.2, 1.3 }, series.TicksBetween(TestTickHelper.Origin.AddSeconds(10), TestTickHelper.Origin.AddSeconds(31)).Select(t => t.Bid));
		Assert.Equal((TestTickHelper.Origin.AddSeconds(5), TestTickHelper.Origin.AddSeconds(30)), (series.FirstTickUtc, series.LastTickUtc));
	}

	[Fact]
	public void Apply_OnEmptyHistory_StartsFirstBar()
	{
		var series = Series();

		series.Apply(TestTickHelper.AtMid(5, 1.1));

		Assert.Single(series.Bars);
	}
}
