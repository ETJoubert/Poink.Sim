using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Simulation;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Simulation.Helpers;

public class SimulationCompareHelperTests
{
	private static CTraderBarRow Bar(int minute, double close) =>
		new(TestTickHelper.Origin.AddMinutes(minute), close, close + 1, close - 1, close, 10);

	private static SimulationCompareSymbol Symbol(string table, params (int Minute, double Close)[] bars) =>
		new(table, "--color-primary", bars.Select(b => Bar(b.Minute, b.Close)).ToList());

	private static readonly SimulationCompareSymbol Traded = Symbol("ticks_a", (0, 100), (1, 110), (2, 120));

	private static readonly SimulationCompareSymbol Watched = Symbol("ticks_b", (1, 50), (2, 40), (3, 60));

	[Fact]
	public void Build_PercentChange_PassesRawClosesForTheChartToRebase()
	{
		var series = SimulationCompareHelper.Build([Traded], SimulationCompareMode.PercentChange, null, null, CompareChartSeriesStyle.Lines);

		Assert.Equal([100.0, 110.0, 120.0], series.Single().Points.Select(p => p.Price));
	}

	[Fact]
	public void Build_WindowAnchored_IsPercentFromTheCloseAtOrBeforeTheAnchor()
	{
		var anchor = TestTickHelper.Origin.AddMinutes(1).AddSeconds(30);

		var series = SimulationCompareHelper.Build([Traded], SimulationCompareMode.WindowAnchored, anchor, null, CompareChartSeriesStyle.Lines);

		var values = series.Single().Points.Select(p => Math.Round(p.Price, 6)).ToList();
		Assert.Equal([Math.Round((100 / 110.0 - 1) * 100, 6), 0, Math.Round((120 / 110.0 - 1) * 100, 6)], values);
	}

	[Fact]
	public void Build_WindowAnchoredBeforeASymbolsFirstBar_IsPercentFromItsFirstBar()
	{
		var series = SimulationCompareHelper.Build(
			[Traded, Watched],
			SimulationCompareMode.WindowAnchored,
			TestTickHelper.Origin,
			null,
			CompareChartSeriesStyle.Lines
		);

		Assert.Equal([0.0, -20.0, 20.0], series[1].Points.Select(p => Math.Round(p.Price, 6)));
	}

	[Fact]
	public void Build_BarsStyle_ScalesEveryPriceOfEachBar()
	{
		var series = SimulationCompareHelper.Build(
			[Traded],
			SimulationCompareMode.WindowAnchored,
			TestTickHelper.Origin,
			null,
			CompareChartSeriesStyle.Bars
		);

		var bar = series.Single().Bars![1];
		Assert.Equal(10, Math.Round(bar.Close, 6));
		Assert.Equal(11, Math.Round(bar.High, 6));
		Assert.Equal(9, Math.Round(bar.Low, 6));
	}

	[Fact]
	public void Build_LinesStyle_HasNoBars()
	{
		var series = SimulationCompareHelper.Build([Traded], SimulationCompareMode.PercentChange, null, null, CompareChartSeriesStyle.Lines);

		Assert.Null(series.Single().Bars);
	}

	[Fact]
	public void Build_WithWatchedRatioTable_AddsLowerPaneRatioJoinedOnBarTime()
	{
		var series = SimulationCompareHelper.Build(
			[Traded, Watched],
			SimulationCompareMode.PercentChange,
			null,
			"ticks_b",
			CompareChartSeriesStyle.Lines
		);

		var ratio = series.Last();
		Assert.Equal(3, series.Count);
		Assert.Equal(1, ratio.Pane);
		Assert.Equal(SimulationCompareHelper.RatioLabel("ticks_a", "ticks_b"), ratio.Label);
		Assert.Equal([110 / 50.0, 120 / 40.0], ratio.Points.Select(p => p.Price));
	}

	[Fact]
	public void Build_WithRatioTableNotWatched_AddsNoRatio()
	{
		var series = SimulationCompareHelper.Build(
			[Traded, Watched],
			SimulationCompareMode.PercentChange,
			null,
			"ticks_a",
			CompareChartSeriesStyle.Lines
		);

		Assert.Equal(2, series.Count);
	}

	[Fact]
	public void BarAtOrBefore_ReturnsLatestBarNotAfterTheTime()
	{
		var bars = new List<CTraderBarRow> { Bar(0, 1), Bar(1, 2), Bar(2, 3) };

		Assert.Equal(2, SimulationCompareHelper.BarAtOrBefore(bars, TestTickHelper.Origin.AddSeconds(90))!.Close);
		Assert.Null(SimulationCompareHelper.BarAtOrBefore(bars, TestTickHelper.Origin.AddSeconds(-1)));
	}
}
