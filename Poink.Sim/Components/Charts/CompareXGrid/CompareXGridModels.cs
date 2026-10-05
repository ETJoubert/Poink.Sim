using System;
using System.Collections.Generic;

namespace Poink.Sim.Components.Charts
{
	/// <summary>How <see cref="CompareXGrid"/> colours a cell several symbols traded in.</summary>
	public enum CompareXGridMode
	{
		/// <summary>The colour of the symbol with the highest share of its own busiest cell, shaded by that share.</summary>
		Dominant,

		/// <summary>The first symbol's share minus one chosen symbol's share, shaded towards whichever is higher.</summary>
		Difference,
	}

	/// <summary>
	/// One symbol's tick counts on a <see cref="CompareXGridResult"/>, row-major with row 0 the highest % band;
	/// <paramref name="MaxCount"/> is its busiest cell, which each cell's share is scaled against.
	/// </summary>
	public sealed record CompareXGridSymbol(string Label, string ColorVar, IReadOnlyList<int> Counts, int MaxCount);

	/// <summary>
	/// Several symbols' footprints on shared % rows and time columns: row r spans
	/// <paramref name="TopPercent"/> − (r+1)·<paramref name="RowPercent"/> to <paramref name="TopPercent"/> − r·<paramref name="RowPercent"/>,
	/// and column c starts at <paramref name="ColumnStarts"/>[c], the last ending at <paramref name="End"/>.
	/// </summary>
	public sealed record CompareXGridResult(
		int RowCount,
		int ColCount,
		double TopPercent,
		double RowPercent,
		IReadOnlyList<DateTime> ColumnStarts,
		DateTime End,
		IReadOnlyList<CompareXGridSymbol> Symbols
	);
}
