using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Above/In/Below mask table over every XGridSignalCatalog entry -- when every enabled key
	/// evaluates true against the live Grid, that's a match, logged with its bar timestamp below.
	/// </summary>
	public partial class XGridRulePanel : ComponentBase
	{
		private const int MaxLogEntries = 20;

		[Parameter]
		public HashSet<string> EnabledKeys { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<string>> EnabledKeysChanged { get; set; }

		[Parameter]
		public XGridResult? Grid { get; set; }

		[Parameter]
		public DateTime? CurrentTimestampUtc { get; set; }

		private double _percent = 10;
		private bool _wasMatching;
		private readonly List<DateTime> _matchLog = new();

		private IReadOnlyList<XGridRuleRow> Rows => XGridRuleCatalog.Rows;

		private bool IsSelected(string? key) => key is not null && EnabledKeys.Contains(key);

		private string Tooltip(string? key) => key is null ? "" : XGridSignalCatalog.Get(key).Tooltip;

		// AND across every enabled key -- the mask matches only when all its constraints hold at once.
		private bool IsMatching =>
			Grid is { } grid
			&& EnabledKeys.Count > 0
			&& EnabledKeys.All(key => XGridSignalCatalog.Value(grid, key, _percent));

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

		// Edge-triggered: logs the bar timestamp a match first appears, not every render it still holds.
		protected override void OnParametersSet()
		{
			var isMatching = IsMatching;
			if (isMatching && !_wasMatching && CurrentTimestampUtc is { } timestamp)
			{
				_matchLog.Insert(0, timestamp);
				if (_matchLog.Count > MaxLogEntries)
					_matchLog.RemoveRange(MaxLogEntries, _matchLog.Count - MaxLogEntries);
			}
			_wasMatching = isMatching;
		}
	}
}
