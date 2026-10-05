using System;
using System.Collections.Generic;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Components.Simulation;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Simulation.Helpers;

public class SimulationSymbolPanelBuilderTests
{
	private static readonly List<Tick> Ticks =
	[
		TestTickHelper.AtMid(0, 10),
		TestTickHelper.AtMid(20, 12),
		TestTickHelper.AtMid(65, 15),
		TestTickHelper.AtMid(90, 14),
		TestTickHelper.AtMid(130, 16),
	];

	private static WatchedSymbolSnapshot Snapshot(bool aggregate = true)
	{
		var feed = new WatchedSymbolFeed(new SimulationWatchSymbol("ticks_w", 1.0), () => Ticks.GetEnumerator());
		feed.Restart(aggregate, TimeSpan.FromMinutes(1));
		feed.AdvanceTo(TestTickHelper.Origin.AddMinutes(10));
		return feed.Snapshot();
	}

	private static SimulationSymbolPanelData Build(
		WatchedSymbolSnapshot symbol,
		(DateTime? Start, DateTime? End) window,
		bool aggregate = true,
		bool xgrid = false,
		bool timeProfile = false
	) =>
		new SimulationSymbolPanelBuilder(new SimulationRowCache()).Build(
			symbol,
			"--color-primary",
			1.0,
			aggregate,
			window,
			new IndicatorSettings(),
			xgrid,
			20,
			20,
			timeProfile
		);

	[Fact]
	public void WindowRange_SelectsBarsOpeningInsideTheEdges()
	{
		var bars = Snapshot().RecentBars;

		Assert.Equal((1, 2), SimulationSymbolPanelBuilder.WindowRange(bars, TestTickHelper.Origin.AddSeconds(30), TestTickHelper.Origin.AddMinutes(5)));
		Assert.Equal((0, 1), SimulationSymbolPanelBuilder.WindowRange(bars, null, TestTickHelper.Origin.AddSeconds(90)));
		Assert.Equal((0, -1), SimulationSymbolPanelBuilder.WindowRange(bars, null, TestTickHelper.Origin.AddSeconds(-1)));
	}

	[Fact]
	public void Build_Aggregated_WindowsTheSymbolsBarsByTime()
	{
		var data = Build(Snapshot(), (TestTickHelper.Origin.AddMinutes(1), TestTickHelper.Origin.AddMinutes(2)));

		Assert.Equal(3, data.Bars.Count);
		Assert.Equal(2, data.WindowBarCount);
		Assert.Equal(TestTickHelper.Origin.AddMinutes(1), data.WindowStartUtc);
		Assert.Equal(TestTickHelper.Origin.AddMinutes(2), data.WindowEndUtc);
		Assert.NotEmpty(data.Samples);
	}

	[Fact]
	public void Build_Aggregated_AnchorsTxFoldJustPastTheWindowsLastBar()
	{
		var data = Build(Snapshot(), (TestTickHelper.Origin, TestTickHelper.Origin.AddMinutes(1)));

		Assert.Equal(TestTickHelper.Origin.AddSeconds(90).AddTicks(1), data.TxFoldAnchorEnd);
		Assert.Equal(14, data.TxFoldLatestPrice);
	}

	[Fact]
	public void Build_WindowBeforeAnyBar_HasNoWindowOrAnchor()
	{
		var data = Build(Snapshot(), (null, TestTickHelper.Origin.AddSeconds(-1)));

		Assert.Equal(0, data.WindowBarCount);
		Assert.Null(data.TxFoldAnchorEnd);
		Assert.Empty(data.Samples);
		Assert.Null(data.XGrid);
	}

	[Fact]
	public void Build_SkipsXGridAndTimeProfileUnlessShown()
	{
		var window = (TestTickHelper.Origin, TestTickHelper.Origin.AddMinutes(2));

		var hidden = Build(Snapshot(), window);
		var shown = Build(Snapshot(), window, xgrid: true, timeProfile: true);

		Assert.Null(hidden.XGrid);
		Assert.Empty(hidden.TimeSamples);
		Assert.NotNull(shown.XGrid);
		Assert.NotEmpty(shown.TimeSamples);
	}

	[Fact]
	public void Build_NotAggregating_HasTickRowsOnly()
	{
		var data = Build(Snapshot(aggregate: false), (null, null), aggregate: false);

		Assert.Empty(data.Bars);
		Assert.Equal(5, data.TickRows.Count);
		Assert.Equal(0, data.WindowBarCount);
	}

	[Fact]
	public void Build_SameInputs_ReturnsTheCachedData()
	{
		var builder = new SimulationSymbolPanelBuilder(new SimulationRowCache());
		var symbol = Snapshot();
		var window = ((DateTime?)TestTickHelper.Origin, (DateTime?)TestTickHelper.Origin.AddMinutes(2));
		var indicators = new IndicatorSettings();

		var first = builder.Build(symbol, "--color-primary", 1.0, true, window, indicators, false, 20, 20, false);
		var second = builder.Build(symbol, "--color-primary", 1.0, true, window, indicators, false, 20, 20, false);

		Assert.Same(first, second);
	}
}
