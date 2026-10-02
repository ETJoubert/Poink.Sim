using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.RuleEngine
{
	/// <summary>Above/In/Below mask over the rule-engine signals (baseline POC/VAH/VAL/VWAP/Median/Regression,
	/// XGrid's rank/delta rows, and TxFold's per-timeframe mean+/-stddev rows) compared against their
	/// live state, with a per-row Match dot and a match log.</summary>
	public partial class RuleEnginePanel : ComponentBase
	{
		private const int MaxLogEntries = 20;

		private static readonly int[] DefaultTxFoldSelectedSeconds = { 60, 120, 240, 480, 900, 1800, 3600, 7200 };

		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Parameter]
		public HashSet<string> EnabledKeys { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<string>> EnabledKeysChanged { get; set; }

		/// <summary>Signal groups switched off here: not calculated, not shown, and their selections ignored.</summary>
		[Parameter]
		public HashSet<RuleSignalGroup> DisabledGroups { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<RuleSignalGroup>> DisabledGroupsChanged { get; set; }

		[Parameter]
		public RuleEngineResult? Result { get; set; }

		[Parameter]
		public double XGridPercent { get; set; } = 10;

		[Parameter]
		public EventCallback<double> XGridPercentChanged { get; set; }

		[Parameter]
		public string? TxFoldTable { get; set; }

		[Parameter]
		public DateTime? TxFoldAnchorEnd { get; set; }

		[Parameter]
		public DateTime? TxFoldRangeStart { get; set; }

		/// <summary>Which TxFold timeframes evaluate -- read-only here, driven by whatever selects it elsewhere (e.g. a TxFold chart's own toolbar).</summary>
		[Parameter]
		public HashSet<int> TxFoldSelectedSeconds { get; set; } = DefaultTxFoldSelectedSeconds.ToHashSet();

		[Parameter]
		public DateTime? CurrentTimestampUtc { get; set; }

		private bool _wasMatching;
		private int _totalMatches;
		private readonly List<DateTime> _matchLog = new();

		private readonly TxFoldBarsCache _txFoldCache = new();

		private IReadOnlyList<RuleSignalRow> Rows =>
			(Result?.Rows ?? Array.Empty<RuleSignalRow>())
				.Concat(
					Result is { } r && !IsDisabled(RuleSignalGroup.TxFold)
						? RuleEngineHelper.TxFoldRows(_txFoldCache.Bars, r.CurrentPrice)
						: Array.Empty<RuleSignalRow>()
				)
				.Where(row => !IsDisabled(row.Group))
				.ToList();

		private bool IsDisabled(RuleSignalGroup group) => DisabledGroups.Contains(group);

		private async Task ToggleGroup(RuleSignalGroup group)
		{
			if (!DisabledGroups.Remove(group))
				DisabledGroups.Add(group);
			await DisabledGroupsChanged.InvokeAsync(DisabledGroups);
		}

		private IEnumerable<RuleSignalRow> BaselineRows => Rows.Where(row => row.Group == RuleSignalGroup.Baseline);

		private IEnumerable<RuleSignalRow> XGridSignalRows => Rows.Where(row => row.Group == RuleSignalGroup.XGrid);

		private IEnumerable<RuleSignalRow> TxFoldSignalRows => Rows.Where(row => row.Group == RuleSignalGroup.TxFold);

		private bool LiveValue(RuleSignalRow row, string key) => RuleEngineHelper.LiveValue(row, key);

		private string? SelectedKey(RuleSignalRow row) => RuleEngineHelper.SelectedKey(row, EnabledKeys);

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

		private bool IsMatching => Result is not null && RuleEngineHelper.Matches(Rows, EnabledKeys);

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

		// The panel can be the only thing on screen using TxFold data, so it loads its own bars instead
		// of relying on the TxFold chart having already fetched them.
		private Task RefreshTxFoldIfNeeded() =>
			IsDisabled(RuleSignalGroup.TxFold)
				? Task.CompletedTask
				: _txFoldCache.RefreshIfNeededAsync(TickRepository, TxFoldTable, TxFoldAnchorEnd, TxFoldRangeStart, TxFoldSelectedSeconds);

		protected override async Task OnParametersSetAsync() => await RefreshTxFoldIfNeeded();

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
