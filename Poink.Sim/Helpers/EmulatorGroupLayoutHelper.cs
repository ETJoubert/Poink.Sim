using System;

namespace Poink.Sim.Helpers;

/// <summary>
/// Lays the group cards out column by column: a column fills to <see cref="CardsPerColumn"/> cards before a second
/// starts, and past <see cref="MaxColumns"/> full columns the columns grow taller instead of adding more.
/// </summary>
internal static class EmulatorGroupLayoutHelper
{
	public const int CardsPerColumn = 4;
	public const int MaxColumns = 2;

	public static (int Columns, int Rows) GridOf(int cards)
	{
		var count = Math.Max(1, cards);
		var columns = Math.Clamp((count + CardsPerColumn - 1) / CardsPerColumn, 1, MaxColumns);
		var rows = Math.Min(count, Math.Max(CardsPerColumn, (count + columns - 1) / columns));
		return (columns, rows);
	}
}
