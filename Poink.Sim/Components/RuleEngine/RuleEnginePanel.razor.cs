using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.RuleEngine
{
	/// <summary>Above/In/Below mask over the rule-engine signals (baseline POC/VAH/VAL/VWAP/Median/Regression,
	/// plus XGrid's rank/delta rows when a grid is supplied) compared against their live state, with a
	/// per-row Match dot and a match log.</summary>
	public partial class RuleEnginePanel : ComponentBase
	{
		private const int MaxLogEntries = 20;

		[Parameter]
		public HashSet<string> EnabledKeys { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<string>> EnabledKeysChanged { get; set; }

		[Parameter]
		public RuleEngineResult? Result { get; set; }

		[Parameter]
		public double XGridPercent { get; set; } = 10;

		[Parameter]
		public EventCallback<double> XGridPercentChanged { get; set; }

		[Parameter]
		public DateTime? CurrentTimestampUtc { get; set; }

		private bool _wasMatching;
		private int _totalMatches;
		private readonly List<DateTime> _matchLog = new();

		private IReadOnlyList<RuleSignalRow> Rows => Result?.Rows ?? Array.Empty<RuleSignalRow>();

		private IEnumerable<RuleSignalRow> BaselineRows => Rows.Where(row => !row.IsXGridSignal);

		private IEnumerable<RuleSignalRow> XGridSignalRows => Rows.Where(row => row.IsXGridSignal);

		private bool LiveValue(RuleSignalRow row, string key) =>
			key == row.AboveKey ? row.Above
			: key == row.InKey ? row.In
			: key == row.BelowKey && row.Below;

		private string? SelectedKey(RuleSignalRow row) =>
			row.AboveKey is { } above && EnabledKeys.Contains(above) ? above
			: EnabledKeys.Contains(row.InKey) ? row.InKey
			: row.BelowKey is { } below && EnabledKeys.Contains(below) ? below
			: null;

		private string MatchStatus(RuleSignalRow row) =>
			SelectedKey(row) is not { } key ? "unset"
			: LiveValue(row, key) ? "match"
			: "no-match";

		private string? LiveKey(RuleSignalRow row) =>
			row.Above ? row.AboveKey
			: row.In ? row.InKey
			: row.Below ? row.BelowKey
			: null;

		private int SelectedCount => Rows.Count(row => SelectedKey(row) is not null);

		private int MatchedCount => Rows.Count(row => MatchStatus(row) == "match");

		// AND across every enabled key -- the mask matches only when all its constraints hold at once.
		private bool IsMatching =>
			Result is not null
			&& EnabledKeys.Count > 0
			&& Rows.All(row => SelectedKey(row) is not { } key || LiveValue(row, key));

		private void ClearRow(RuleSignalRow row)
		{
			if (row.AboveKey is { } above)
				EnabledKeys.Remove(above);
			EnabledKeys.Remove(row.InKey);
			if (row.BelowKey is { } below)
				EnabledKeys.Remove(below);
		}

		private async Task Toggle(RuleSignalRow row, string? key)
		{
			if (key is null)
				return;
			var wasSelected = SelectedKey(row) == key;
			ClearRow(row);
			if (!wasSelected)
				EnabledKeys.Add(key);
			await EnabledKeysChanged.InvokeAsync(EnabledKeys);
		}

		private async Task CopyLiveToSelector()
		{
			if (Result is null)
				return;
			foreach (var row in Rows)
			{
				ClearRow(row);
				if (LiveKey(row) is { } key)
					EnabledKeys.Add(key);
			}
			await EnabledKeysChanged.InvokeAsync(EnabledKeys);
		}

		private async Task SetXGridPercent(ChangeEventArgs e)
		{
			if (double.TryParse(e.Value?.ToString(), out var value))
				await XGridPercentChanged.InvokeAsync(value);
		}

		// Edge-triggered: logs the timestamp a match first appears, not every render it still holds.
		protected override void OnParametersSet()
		{
			var isMatching = IsMatching;
			if (isMatching && !_wasMatching && CurrentTimestampUtc is { } timestamp)
			{
				_totalMatches++;
				_matchLog.Insert(0, timestamp);
				if (_matchLog.Count > MaxLogEntries)
					_matchLog.RemoveRange(MaxLogEntries, _matchLog.Count - MaxLogEntries);
			}
			_wasMatching = isMatching;
		}
	}
}
