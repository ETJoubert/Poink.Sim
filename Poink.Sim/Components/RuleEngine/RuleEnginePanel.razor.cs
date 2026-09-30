using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.RuleEngine
{
	/// <summary>Above/In/Below mask over the baseline rule-engine signals (POC/VAH/VAL/VWAP) compared
	/// against their live state, with a per-row Match dot and a match log.</summary>
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
		public DateTime? CurrentTimestampUtc { get; set; }

		private bool _wasMatching;
		private int _totalMatches;
		private readonly List<DateTime> _matchLog = new();

		private IReadOnlyList<RuleSignalRow> Rows => Result?.Rows ?? Array.Empty<RuleSignalRow>();

		private bool LiveValue(RuleSignalRow row, string key) =>
			key == row.AboveKey ? row.Above
			: key == row.InKey ? row.In
			: key == row.BelowKey && row.Below;

		private string? SelectedKey(RuleSignalRow row) =>
			EnabledKeys.Contains(row.AboveKey) ? row.AboveKey
			: EnabledKeys.Contains(row.InKey) ? row.InKey
			: EnabledKeys.Contains(row.BelowKey) ? row.BelowKey
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

		private async Task Toggle(RuleSignalRow row, string key)
		{
			var wasSelected = SelectedKey(row) == key;
			EnabledKeys.Remove(row.AboveKey);
			EnabledKeys.Remove(row.InKey);
			EnabledKeys.Remove(row.BelowKey);
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
				EnabledKeys.Remove(row.AboveKey);
				EnabledKeys.Remove(row.InKey);
				EnabledKeys.Remove(row.BelowKey);
				if (LiveKey(row) is { } key)
					EnabledKeys.Add(key);
			}
			await EnabledKeysChanged.InvokeAsync(EnabledKeys);
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
