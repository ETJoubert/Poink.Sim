using System;
using System.Collections.Generic;
using System.Globalization;

namespace Poink.Sim.Components.Shared.WeekRangeGrid;

/// <summary>One Monday-aligned week of the selectable range, trimmed to the range's own bounds at either end.</summary>
public readonly record struct WeekCell(DateTime Start, DateTime End);

/// <summary>One month marker under the week grid, placed at the first week that starts in that month.</summary>
public readonly record struct WeekLabel(int CellIndex, string Text);

internal static class WeekRangeHelper
{
	private const int MaxLabels = 12;

	public static List<WeekCell> BuildCells(DateTime min, DateTime max)
	{
		var cells = new List<WeekCell>();
		if (max <= min)
			return cells;

		for (var weekStart = MondayOf(min); weekStart < max; weekStart = weekStart.AddDays(7))
		{
			var start = weekStart < min ? min : weekStart;
			var nextWeek = weekStart.AddDays(7);
			cells.Add(new WeekCell(start, nextWeek > max ? max : nextWeek));
		}
		return cells;
	}

	/// <summary>The cell holding <paramref name="time"/>; an end time sitting exactly on a boundary belongs to the week it closes.</summary>
	public static int IndexOf(IReadOnlyList<WeekCell> cells, DateTime time, bool isEnd)
	{
		for (var i = 0; i < cells.Count; i++)
		{
			var inside = isEnd ? time > cells[i].Start && time <= cells[i].End : time >= cells[i].Start && time < cells[i].End;
			if (inside)
				return i;
		}
		return time <= cells[0].Start ? 0 : cells.Count - 1;
	}

	public static List<WeekLabel> BuildLabels(IReadOnlyList<WeekCell> cells)
	{
		var monthStarts = new List<int>();
		for (var i = 0; i < cells.Count; i++)
			if (i == 0 || cells[i].Start.Month != cells[i - 1].Start.Month)
				monthStarts.Add(i);

		var step = Math.Max(1, (int)Math.Ceiling(monthStarts.Count / (double)MaxLabels));
		var labels = new List<WeekLabel>();
		for (var n = 0; n < monthStarts.Count; n += step)
		{
			var start = cells[monthStarts[n]].Start;
			var text = n == 0 || start.Month == 1
				? start.ToString("MMM yy", CultureInfo.InvariantCulture)
				: start.ToString("MMM", CultureInfo.InvariantCulture);
			labels.Add(new WeekLabel(monthStarts[n], text));
		}
		return labels;
	}

	private static DateTime MondayOf(DateTime time)
	{
		var daysSinceMonday = ((int)time.DayOfWeek + 6) % 7;
		return DateTime.SpecifyKind(time.Date.AddDays(-daysSinceMonday), time.Kind);
	}
}
