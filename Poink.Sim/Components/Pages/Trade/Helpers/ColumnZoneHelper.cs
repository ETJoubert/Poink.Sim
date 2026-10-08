using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Models;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Reads XGrid's recent columns into price spans and places the current price in each one's low, middle or high band.</summary>
	public static class ColumnZoneHelper
	{
		public const double MinZonePercent = 1;

		/// <summary>At this share the low and high bands meet and the middle band vanishes.</summary>
		public const double MaxZonePercent = 50;

		public const double DefaultZonePercent = 25;

		public const int MaxColumnsBack = 20;

		public const int DefaultColumnsBack = 1;

		public static readonly ColumnZoneSettings DefaultSettings = new(DefaultColumnsBack, DefaultZonePercent, false, 1);

		public static int ClampColumnsBack(int columnsBack) => Math.Clamp(columnsBack, 0, MaxColumnsBack);

		/// <summary>h runs from 1 (any one supporting column unlocks) to every shown column.</summary>
		public static int ClampGateThreshold(int threshold, int columnCount) => Math.Clamp(threshold, 1, Math.Max(1, columnCount));

		/// <summary>A column supports LONG only with the price at its bottom; MID and an unreadable column never count.</summary>
		public static bool SupportsLong(ColumnZone? zone) => zone is ColumnZone.Low or ColumnZone.Below;

		/// <summary>A column supports SHORT only with the price at its top; MID and an unreadable column never count.</summary>
		public static bool SupportsShort(ColumnZone? zone) => zone is ColumnZone.High or ColumnZone.Above;

		public static ColumnZoneGate Gate(IReadOnlyList<ColumnZoneRange?> ranges, ColumnZoneSettings settings)
		{
			var zones = ranges.Select(r => r is { } range ? ZoneOf(range, settings.ZonePercent) : (ColumnZone?)null).ToList();
			return new ColumnZoneGate(
				settings.GateEnabled,
				zones.Count(SupportsLong),
				zones.Count(SupportsShort),
				ClampGateThreshold(settings.GateThreshold, zones.Count),
				zones.Count
			);
		}

		public static ColumnZoneGate Gate(XGridResult? grid, ColumnZoneSettings settings) =>
			Gate(RecentColumns(grid, settings.ColumnsBack), settings);

		/// <summary>"n" for the latest column, "n-k" for the column k before it.</summary>
		public static string ColumnLabel(int columnsBack) => columnsBack == 0 ? "n" : $"n-{columnsBack}";

		/// <summary>Columns n through n-<paramref name="columnsBack"/>, latest first; a column the grid lacks is null.</summary>
		public static IReadOnlyList<ColumnZoneRange?> RecentColumns(XGridResult? grid, int columnsBack) =>
			Enumerable.Range(0, ClampColumnsBack(columnsBack) + 1).Select(k => ColumnRange(grid, k)).ToList();

		/// <summary>
		/// The occupied price span of the column <paramref name="columnsBack"/> from the right-most one, paired with XGrid's
		/// current price; null until the grid has that column traded and a current price.
		/// </summary>
		public static ColumnZoneRange? ColumnRange(XGridResult? grid, int columnsBack)
		{
			if (grid is not { } g || columnsBack < 0 || columnsBack >= g.Columns.Count || g.Current is not { } current)
				return null;

			var occupiedRows = g.Columns[g.Columns.Count - 1 - columnsBack]
				.Cells.Select((cell, row) => (cell, row))
				.Where(c => c.cell.Count > 0)
				.ToList();
			if (occupiedRows.Count == 0)
				return null;

			var topRow = occupiedRows[0].row;
			var bottomRow = occupiedRows[^1].row;
			return new ColumnZoneRange(g.RowLow[bottomRow], g.RowHigh[topRow], current.Price);
		}

		/// <summary>The price's place along the span, 0 at the low and 1 at the high; a flat span reads as the middle.</summary>
		public static double PricePosition(ColumnZoneRange range) =>
			range.Span > 0 ? Math.Clamp((range.Price - range.Low) / range.Span, 0, 1) : 0.5;

		public static double ClampZonePercent(double percent) =>
			double.IsFinite(percent) ? Math.Clamp(Math.Round(percent), MinZonePercent, MaxZonePercent) : DefaultZonePercent;

		/// <summary>Below or Above when the price is outside the span, otherwise its band by <see cref="ZoneOf(double, double)"/>.</summary>
		public static ColumnZone ZoneOf(ColumnZoneRange range, double zonePercent)
		{
			if (range.Price < range.Low)
				return ColumnZone.Below;

			return range.Price > range.High ? ColumnZone.Above : ZoneOf(PricePosition(range), zonePercent);
		}

		/// <summary>Low within the bottom <paramref name="zonePercent"/>% of the span, High within the top, Middle between.</summary>
		public static ColumnZone ZoneOf(double position, double zonePercent)
		{
			var edge = ClampZonePercent(zonePercent) / 100;
			if (position <= edge)
				return ColumnZone.Low;

			return position >= 1 - edge ? ColumnZone.High : ColumnZone.Middle;
		}
	}
}
