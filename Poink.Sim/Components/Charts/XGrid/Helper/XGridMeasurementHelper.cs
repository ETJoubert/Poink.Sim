using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Poink.Sim.Components.Shared.Measurements;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Turns a selected cell, row or column of an XGrid into the measurements the measurements panel
	/// lists. A cell only counts as active when it holds ticks.
	/// </summary>
	public static class XGridMeasurementHelper
	{
		public static string Title(IReadOnlyList<XGridSelection> selections) =>
			selections.Count > 0 ? $"{selections[0].Kind.ToString().ToUpperInvariant()} MEASUREMENTS" : "MEASUREMENTS";

		public static IReadOnlyList<MeasurementColumn> MeasureAll(XGridResult grid, IReadOnlyList<XGridSelection> selections) =>
			[.. selections.Select(s => new MeasurementColumn(s.Label, Measure(grid, s)))];

		public static IReadOnlyList<MeasurementItem> Measure(XGridResult grid, XGridSelection? selection)
		{
			if (selection is not { } s || grid.RowLow.Count == 0 || grid.Columns.Count == 0)
				return [];
			return s.Kind switch
			{
				XGridSelectionKind.Cell => MeasureCell(grid, s),
				XGridSelectionKind.Row => MeasureRow(grid, s.Row),
				_ => MeasureColumn(grid, s.Col),
			};
		}

		private static IReadOnlyList<MeasurementItem> MeasureCell(XGridResult grid, XGridSelection s)
		{
			if (!InRange(grid, s.Row, s.Col))
				return [];
			XGridCell cell = grid.Columns[s.Col].Cells[s.Row];
			int delta = cell.UpCount - cell.DownCount;
			return
			[
				Item("Time", grid.Columns[s.Col].OpenTime.ToString("HH:mm", CultureInfo.InvariantCulture), "Open time of the cell's column"),
				Item("Price", Price(RowMid(grid, s.Row)), "Middle price of the cell"),
				Item("Ticks", Count(cell.Count), "Total ticks in the cell"),
				Item("Up ticks", Count(cell.UpCount), "Ticks that moved price up"),
				Item("Down ticks", Count(cell.DownCount), "Ticks that moved price down"),
				Item("Delta", Signed(delta), "Up ticks minus down ticks"),
				Item("Up %", Percent(cell.UpCount, cell.Count), "Share of the cell's ticks that moved up"),
				Item("Up dist", Price(cell.UpDistance), "Total price distance travelled by up ticks"),
				Item("Down dist", Price(cell.DownDistance), "Total price distance travelled by down ticks"),
				Item("Delta dist", SignedPrice(cell.UpDistance - cell.DownDistance), "Up distance minus down distance"),
			];
		}

		private static IReadOnlyList<MeasurementItem> MeasureRow(XGridResult grid, int row)
		{
			if (row < 0 || row >= grid.RowLow.Count)
				return [];
			var cells = new List<XGridCell>();
			foreach (var column in grid.Columns)
				cells.Add(column.Cells[row]);
			return
			[
				Item("Price", Price(RowMid(grid, row)), "Middle price of the row"),
				.. Aggregate(cells, "columns"),
			];
		}

		private static IReadOnlyList<MeasurementItem> MeasureColumn(XGridResult grid, int col)
		{
			if (col < 0 || col >= grid.Columns.Count)
				return [];
			XGridColumn column = grid.Columns[col];
			int top = -1,
				bottom = -1;
			for (var r = 0; r < column.Cells.Count; r++)
			{
				if (column.Cells[r].Count == 0)
					continue;
				if (top < 0)
					top = r;
				bottom = r;
			}
			string price = top < 0 ? "--" : Price((grid.RowHigh[top] + grid.RowLow[bottom]) / 2);
			string span = top < 0 ? "--" : Price(grid.RowHigh[top] - grid.RowLow[bottom]);
			return
			[
				Item("Time", column.OpenTime.ToString("HH:mm", CultureInfo.InvariantCulture), "Open time of the column"),
				Item("Price", price, "Middle of the lowest and highest price of the cells that hold ticks"),
				Item("Span", span, "Price distance between that lowest and highest price"),
				.. Aggregate(column.Cells, "rows"),
			];
		}

		private static List<MeasurementItem> Aggregate(IReadOnlyList<XGridCell> cells, string across)
		{
			int total = 0,
				up = 0,
				down = 0,
				active = 0,
				max = 0,
				min = int.MaxValue;
			foreach (var cell in cells)
			{
				if (cell.Count == 0)
					continue;
				active++;
				total += cell.Count;
				up += cell.UpCount;
				down += cell.DownCount;
				max = Math.Max(max, cell.Count);
				min = Math.Min(min, cell.Count);
			}
			return
			[
				Item("Ticks", Count(total), "Total ticks"),
				Item("Up ticks", Count(up), "Ticks that moved price up"),
				Item("Down ticks", Count(down), "Ticks that moved price down"),
				Item("Delta", Signed(up - down), "Up ticks minus down ticks"),
				Item("Cells", $"{active} / {cells.Count}", $"Cells holding ticks out of all cells across the {across}"),
				Item("Max cell", active == 0 ? "--" : Count(max), "Most ticks in one active cell"),
				Item("Min cell", active == 0 ? "--" : Count(min), "Fewest ticks in one active cell"),
				Item("Avg cell", active == 0 ? "--" : (total / (double)active).ToString("N1", CultureInfo.InvariantCulture), "Average ticks per active cell"),
			];
		}

		private static bool InRange(XGridResult grid, int row, int col) =>
			row >= 0 && row < grid.RowLow.Count && col >= 0 && col < grid.Columns.Count;

		private static MeasurementItem Item(string label, string value, string tooltip) => new(label, value, tooltip);

		private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

		private static string Signed(int value) => value.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);

		private static string Price(double value) => value.ToString("F5", CultureInfo.InvariantCulture);

		private static string SignedPrice(double value) =>
			value.ToString("+0.00000;-0.00000;0.00000", CultureInfo.InvariantCulture);

		private static double RowMid(XGridResult grid, int row) => (grid.RowLow[row] + grid.RowHigh[row]) / 2;

		private static string Percent(int part, int whole) =>
			whole == 0 ? "--" : (part * 100.0 / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";
	}
}
