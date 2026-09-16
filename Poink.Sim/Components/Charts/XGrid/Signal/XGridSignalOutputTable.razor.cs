using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>Read-only Above/In/Below readout over XGridRuleCatalog rows.</summary>
	public partial class XGridSignalOutputTable : ComponentBase
	{
		// Null shows every catalog row; a subset only shows rows/cells for those specific keys.
		[Parameter]
		public IReadOnlyList<string>? Keys { get; set; }

		[Parameter]
		public Func<string, bool> ValueOf { get; set; } = _ => false;

		private IReadOnlyList<XGridRuleRow> Rows =>
			Keys is null
				? XGridRuleCatalog.Rows
				: XGridRuleCatalog.BuildRows(XGridSignalCatalog.All.Where(d => Keys.Contains(d.Key)));

		private string Tooltip(string? key) => key is null ? "" : XGridSignalCatalog.Get(key).Tooltip;
	}
}
