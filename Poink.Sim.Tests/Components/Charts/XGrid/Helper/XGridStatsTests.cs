using System;
using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Charts;

public class XGridStatsTests
{
	private const double UnitWidth = 1.0;

	private static HlocAvdtBar Bar(int minute)
	{
		var open = TestTickHelper.Origin.AddMinutes(minute);
		return new HlocAvdtBar(open, open.AddSeconds(59), open, open, 11, 12, 10, 11, 11, 1, 0, 0, 0);
	}

	private static BarPriceHistogram Histogram(params (double Price, double Seconds)[] visits)
	{
		var histogram = new BarPriceHistogram(UnitWidth);
		foreach (var (price, seconds) in visits)
		{
			histogram.Add(price, 1, 0);
			histogram.AddDwell(price, seconds);
		}
		return histogram;
	}

	private static XGridResult ComputeSingleColumn()
	{
		var bars = new List<HlocAvdtBar> { Bar(0), Bar(1) };
		var histograms = new List<BarPriceHistogram>
		{
			Histogram((10.5, 30), (10.5, 15)),
			Histogram((11.5, 120), (10.5, 5)),
		};

		var grid = XGridStats.Compute(
			bars,
			histograms,
			new List<WeightedPriceSample>(),
			DateTime.MinValue,
			DateTime.MaxValue,
			UnitWidth,
			colCount: 1
		);

		Assert.NotNull(grid);
		return grid.Value;
	}

	private static XGridCell CellAt(XGridResult grid, double price)
	{
		for (var r = 0; r < grid.RowLow.Count; r++)
			if (price >= grid.RowLow[r] && price < grid.RowHigh[r])
				return grid.Columns[0].Cells[r];
		throw new InvalidOperationException($"No row holds {price}");
	}

	private static XGridCell Cell(int count, double seconds) => new(count, 0, 0, 0, 0, seconds, count);

	[Fact]
	public void Compute_SumsBookedDwellSecondsPerCell()
	{
		var grid = ComputeSingleColumn();

		Assert.Equal(50, CellAt(grid, 10.5).Seconds, 6);
		Assert.Equal(120, CellAt(grid, 11.5).Seconds, 6);
		Assert.Equal(0, CellAt(grid, 12.5).Seconds, 6);
	}

	[Fact]
	public void Compute_TracksTheLongestCellAsMaxSeconds()
	{
		Assert.Equal(120, ComputeSingleColumn().MaxSeconds, 6);
	}

	[Theory]
	[InlineData(XGridDwellComparison.Above, 60, true)]
	[InlineData(XGridDwellComparison.Above, 59, false)]
	[InlineData(XGridDwellComparison.Below, 60, true)]
	[InlineData(XGridDwellComparison.Below, 61, false)]
	public void MatchesDwell_IncludesTheThresholdOnEitherSide(XGridDwellComparison comparison, double cellSeconds, bool expected)
	{
		Assert.Equal(expected, XGridStats.MatchesDwell(Cell(1, cellSeconds), comparison, 60));
	}

	[Fact]
	public void MatchesDwell_Below_SkipsCellsPriceNeverVisited()
	{
		Assert.False(XGridStats.MatchesDwell(Cell(0, 0), XGridDwellComparison.Below, 60));
	}

	[Fact]
	public void MatchesDwell_Below_CountsATimeOnlyCellAsVisited()
	{
		Assert.True(XGridStats.MatchesDwell(Cell(0, 15), XGridDwellComparison.Below, 60));
	}
}
