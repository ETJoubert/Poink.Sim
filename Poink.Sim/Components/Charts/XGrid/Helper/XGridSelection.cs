namespace Poink.Sim.Components.Charts
{
	public enum XGridSelectionKind
	{
		Cell,
		Row,
		Column,
	}

	/// <summary>A selected cell, row or column by grid index; Row is ignored for a column and Col for a row.</summary>
	public readonly record struct XGridSelection(XGridSelectionKind Kind, int Row, int Col)
	{
		/// <summary>Short 1-based name, counting rows from the top and columns from the left.</summary>
		public string Label =>
			Kind switch
			{
				XGridSelectionKind.Cell => $"r{Row + 1}c{Col + 1}",
				XGridSelectionKind.Row => $"Row {Row + 1}",
				_ => $"Col {Col + 1}",
			};
	}
}
