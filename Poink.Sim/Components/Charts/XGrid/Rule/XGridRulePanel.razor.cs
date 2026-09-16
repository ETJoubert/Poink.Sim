using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Above/In/Below mask table over every XGridSignalCatalog entry -- purely an input editor,
	/// the enabled key set is not yet compared against a live grid's actual signals.
	/// </summary>
	public partial class XGridRulePanel : ComponentBase
	{
		[Parameter]
		public HashSet<string> EnabledKeys { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<string>> EnabledKeysChanged { get; set; }

		private IReadOnlyList<XGridRuleRow> Rows => XGridRuleCatalog.Rows;

		private bool IsSelected(string? key) => key is not null && EnabledKeys.Contains(key);

		private string Tooltip(string? key) => key is null ? "" : XGridSignalCatalog.Get(key).Tooltip;

		// Above/In/Below are mutually exclusive per row; re-selecting the current one clears it back to unset.
		private async Task Select(XGridRuleRow row, string key)
		{
			var wasSelected = IsSelected(key);
			if (row.AboveKey is not null)
				EnabledKeys.Remove(row.AboveKey);
			if (row.InKey is not null)
				EnabledKeys.Remove(row.InKey);
			if (row.BelowKey is not null)
				EnabledKeys.Remove(row.BelowKey);
			if (!wasSelected)
				EnabledKeys.Add(key);
			await EnabledKeysChanged.InvokeAsync(EnabledKeys);
		}
	}
}
