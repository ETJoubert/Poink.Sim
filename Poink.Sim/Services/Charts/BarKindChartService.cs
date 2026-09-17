using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Services.Charts;

internal interface IBarKindChartService
{
	Task<(List<CTraderBarRow> Bars, List<ChartLinePoint> MedianLine)> BuildBarsAsync(
		string table,
		DateTime from,
		DateTime to,
		int barWidthSeconds,
		BarAggregationKind kind
	);
}

// Shared by Analize's Price tab and Profiler's Subchart panel -- both aggregate an arbitrary tick
// range into an OHLC/Quartile bar chart the same way, over an already-bounded time window.
internal sealed class BarKindChartService : IBarKindChartService
{
	private const double PriceBucketWidth = 0.01;

	private readonly ITickRepository _tickRepository;

	public BarKindChartService(ITickRepository tickRepository) => _tickRepository = tickRepository;

	public Task<(List<CTraderBarRow> Bars, List<ChartLinePoint> MedianLine)> BuildBarsAsync(
		string table,
		DateTime from,
		DateTime to,
		int barWidthSeconds,
		BarAggregationKind kind
	) =>
		Task.Run(() =>
			kind == BarAggregationKind.Quartile
				? BuildQuartileBars(table, from, to, barWidthSeconds)
				: (BuildOhlcBars(table, from, to, barWidthSeconds), new List<ChartLinePoint>())
		);

	private List<CTraderBarRow> BuildOhlcBars(string table, DateTime from, DateTime to, int barWidthSeconds)
	{
		var aggregator = new SimulationBarAggregator(TimeSpan.FromSeconds(barWidthSeconds), PriceBucketWidth);
		var bars = new List<HlocAvdtBar>();
		foreach (var tick in _tickRepository.ReadTicks(table, from, to))
			if (aggregator.Feed(tick) is { } completed)
				bars.Add(completed.Bar);
		if (aggregator.CurrentPartialBar is { } partial)
			bars.Add(partial);

		return bars
			.Select(b => new CTraderBarRow(b.OpenTime, b.Open, b.High, b.Low, b.Close, b.Volume, b.UpVolume, b.DownVolume, b.Distance))
			.ToList();
	}

	// Reuses the candlestick chart as a box plot (Open/Close = Q1/Q3, High/Low = Min/Max); Median rides along as its own overlay line.
	private (List<CTraderBarRow> Bars, List<ChartLinePoint> MedianLine) BuildQuartileBars(
		string table,
		DateTime from,
		DateTime to,
		int barWidthSeconds
	)
	{
		var aggregator = new QuartileBarAggregator(TimeSpan.FromSeconds(barWidthSeconds));
		var bars = new List<QuartileBar>();
		foreach (var tick in _tickRepository.ReadTicks(table, from, to))
			if (aggregator.Feed(tick) is { } completed)
				bars.Add(completed);
		if (aggregator.CurrentPartialBar is { } partial)
			bars.Add(partial);

		var rows = bars.Select(b => new CTraderBarRow(b.OpenTime, b.Q1, b.Max, b.Min, b.Q3, b.Count)).ToList();
		var medianLine = bars.Select(b => new ChartLinePoint(b.OpenTime, b.Median)).ToList();
		return (rows, medianLine);
	}
}
