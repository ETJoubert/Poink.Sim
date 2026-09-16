using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>Above/In/Below mask table with the live grid's state and a per-row match dot alongside it.</summary>
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

		// The live grid's own current state for one direction, independent of what's selected.
		private bool LiveValue(string? key) => key is not null && Grid is { } grid && XGridSignalCatalog.Value(grid, key, _percent);

		private string? SelectedKey(XGridRuleRow row) =>
			row.AboveKey is { } above && EnabledKeys.Contains(above) ? above
			: row.InKey is { } inKey && EnabledKeys.Contains(inKey) ? inKey
			: row.BelowKey is { } below && EnabledKeys.Contains(below) ? below
			: null;

		private string MatchStatus(XGridRuleRow row)
		{
			if (SelectedKey(row) is not { } key)
				return "unset";
			return LiveValue(key) ? "match" : "no-match";
		}

		// Whichever of a row's three keys is currently true against the live grid, if any.
		private string? LiveKey(XGridRuleRow row) =>
			row.AboveKey is { } above && LiveValue(above) ? above
			: row.InKey is { } inKey && LiveValue(inKey) ? inKey
			: row.BelowKey is { } below && LiveValue(below) ? below
			: null;

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

		private async Task CopyLiveToSelector()
		{
			if (Grid is null)
				return;
			foreach (var row in Rows)
			{
				if (row.AboveKey is not null)
					EnabledKeys.Remove(row.AboveKey);
				if (row.InKey is not null)
					EnabledKeys.Remove(row.InKey);
				if (row.BelowKey is not null)
					EnabledKeys.Remove(row.BelowKey);
				if (LiveKey(row) is { } key)
					EnabledKeys.Add(key);
			}
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
