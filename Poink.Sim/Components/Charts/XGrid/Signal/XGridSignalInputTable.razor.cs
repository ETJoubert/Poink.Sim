using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	// Exclusive: Above/In/Below are mutually exclusive per row (Rule Engine's current-state
	// selector). Independent: each cell toggles on its own (Brain Builder's feature picker).
	public enum XGridSignalInputMode
	{
		Exclusive,
		Independent,
	}

	/// <summary>Above/In/Below toggle table over every XGridRuleCatalog row -- shared by the Rule Engine and Brain Builder.</summary>
	public partial class XGridSignalInputTable : ComponentBase
	{
		[Parameter]
		public HashSet<string> EnabledKeys { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<string>> EnabledKeysChanged { get; set; }

		[Parameter]
		public XGridSignalInputMode Mode { get; set; } = XGridSignalInputMode.Exclusive;

		[Parameter]
		public bool Disabled { get; set; }

		private IReadOnlyList<XGridRuleRow> Rows => XGridRuleCatalog.Rows;

		private bool IsSelected(string? key) => key is not null && EnabledKeys.Contains(key);

		private string Tooltip(string? key) => key is null ? "" : XGridSignalCatalog.Get(key).Tooltip;

		private async Task Toggle(XGridRuleRow row, string key)
		{
			if (Mode == XGridSignalInputMode.Independent)
			{
				if (!EnabledKeys.Remove(key))
					EnabledKeys.Add(key);
				await EnabledKeysChanged.InvokeAsync(EnabledKeys);
				return;
			}

			// Exclusive: re-selecting the current one clears the row back to unset.
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
