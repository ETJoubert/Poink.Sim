using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// One dot per XGridRuleCatalog row -- green when that row's selected constraint currently
	/// matches the live grid, red when it doesn't, grey when the row has no selection.
	/// </summary>
	public partial class XGridRuleMatchBar : ComponentBase
	{
		[Parameter]
		public HashSet<string> EnabledKeys { get; set; } = new();

		[Parameter]
		public XGridResult? Grid { get; set; }

		[Parameter]
		public double Percent { get; set; } = 10;

		private IReadOnlyList<XGridRuleRow> Rows => XGridRuleCatalog.Rows;

		private string? SelectedKey(XGridRuleRow row) =>
			row.AboveKey is { } above && EnabledKeys.Contains(above) ? above
			: row.InKey is { } inKey && EnabledKeys.Contains(inKey) ? inKey
			: row.BelowKey is { } below && EnabledKeys.Contains(below) ? below
			: null;

		private string Status(XGridRuleRow row)
		{
			if (SelectedKey(row) is not { } key)
				return "unset";
			return Grid is { } grid && XGridSignalCatalog.Value(grid, key, Percent) ? "match" : "no-match";
		}

		private string Title(XGridRuleRow row) =>
			Status(row) switch
			{
				"match" => $"{row.Label}: matches the live grid",
				"no-match" => $"{row.Label}: does not match the live grid",
				_ => $"{row.Label}: no constraint selected",
			};
	}
}
