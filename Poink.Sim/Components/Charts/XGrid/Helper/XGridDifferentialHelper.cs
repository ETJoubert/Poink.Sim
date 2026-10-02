using System.Collections.Generic;
using Poink.Sim.Components.Shared.Differentials;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Adapts XGrid selections to the generic differentials panel: each selection becomes a point and a
	/// set of tick metrics, and consecutive selections (in click order) become the pairs to measure.
	/// </summary>
	public static class XGridDifferentialHelper
	{
		public static IReadOnlyList<DifferentialPair> BuildPairs(XGridResult grid, IReadOnlyList<XGridSelection> selections)
		{
			var pairs = new List<DifferentialPair>();
			for (var i = 1; i < selections.Count; i++)
			{
				if (Describe(grid, selections[i - 1]) is not { } from || Describe(grid, selections[i]) is not { } to)
					continue;
				pairs.Add(new DifferentialPair($"{from.Point.Label} → {to.Point.Label}", from.Point, to.Point, Metrics(from, to)));
			}
			return pairs;
		}

		private readonly record struct Described(DifferentialPoint Point, int Ticks, int Up, int Down);

		private static DifferentialMetric[] Metrics(Described from, Described to) =>
			[
				new("Ticks", from.Ticks, to.Ticks),
				new("Up ticks", from.Up, to.Up),
				new("Down ticks", from.Down, to.Down),
				new("Delta", from.Up - from.Down, to.Up - to.Down),
			];

		private static Described? Describe(XGridResult grid, XGridSelection selection) =>
			selection.Kind switch
			{
				XGridSelectionKind.Cell => DescribeCell(grid, selection.Row, selection.Col),
				XGridSelectionKind.Row => DescribeRow(grid, selection.Row),
				_ => DescribeColumn(grid, selection.Col),
			};

		private static Described? DescribeCell(XGridResult grid, int row, int col)
		{
			if (row < 0 || row >= grid.RowLow.Count || col < 0 || col >= grid.Columns.Count)
				return null;
			XGridCell cell = grid.Columns[col].Cells[row];
			var point = new DifferentialPoint(new XGridSelection(XGridSelectionKind.Cell, row, col).Label, RowMid(grid, row), grid.Columns[col].OpenTime);
			return new Described(point, cell.Count, cell.UpCount, cell.DownCount);
		}

		private static Described? DescribeRow(XGridResult grid, int row)
		{
			if (row < 0 || row >= grid.RowLow.Count)
				return null;
			int ticks = 0,
				up = 0,
				down = 0;
			foreach (var column in grid.Columns)
			{
				XGridCell cell = column.Cells[row];
				ticks += cell.Count;
				up += cell.UpCount;
				down += cell.DownCount;
			}
			return new Described(new DifferentialPoint(new XGridSelection(XGridSelectionKind.Row, row, 0).Label, RowMid(grid, row)), ticks, up, down);
		}

		// A column spans prices, so its point sits at the middle of the cells that hold ticks.
		private static Described? DescribeColumn(XGridResult grid, int col)
		{
			if (col < 0 || col >= grid.Columns.Count || grid.RowLow.Count == 0)
				return null;
			XGridColumn column = grid.Columns[col];
			int top = -1,
				bottom = -1,
				ticks = 0,
				up = 0,
				down = 0;
			for (var r = 0; r < column.Cells.Count; r++)
			{
				XGridCell cell = column.Cells[r];
				if (cell.Count == 0)
					continue;
				if (top < 0)
					top = r;
				bottom = r;
				ticks += cell.Count;
				up += cell.UpCount;
				down += cell.DownCount;
			}
			double price = top < 0 ? (grid.RowHigh[0] + grid.RowLow[^1]) / 2 : (grid.RowHigh[top] + grid.RowLow[bottom]) / 2;
			return new Described(new DifferentialPoint(new XGridSelection(XGridSelectionKind.Column, 0, col).Label, price, column.OpenTime), ticks, up, down);
		}

		private static double RowMid(XGridResult grid, int row) => (grid.RowLow[row] + grid.RowHigh[row]) / 2;
	}
}
