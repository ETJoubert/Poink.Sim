using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Helpers;

/// <summary>
/// Loads and caches the TxFold mean/stddev bars the rule engine's TxFold rows read, refetching only when
/// the anchor, range or selected timeframes change -- shared by the rule panel and the robot so neither
/// relies on a chart having already loaded the data.
/// </summary>
internal sealed class TxFoldBarsCache
{
	private (string Table, DateTime AnchorEnd, DateTime RangeStart, string SelectedKey)? _loadedFor;
	private bool _loading;

	public IReadOnlyList<TxFoldBar> Bars { get; private set; } = Array.Empty<TxFoldBar>();

	public async Task RefreshIfNeededAsync(
		ITickRepository tickRepository,
		string? table,
		DateTime? anchorEnd,
		DateTime? rangeStart,
		IReadOnlyCollection<int> selectedSeconds
	)
	{
		if (
			string.IsNullOrWhiteSpace(table)
			|| anchorEnd is not { } anchor
			|| rangeStart is not { } start
			|| selectedSeconds.Count == 0
			|| _loading
		)
			return;

		var selectedKey = string.Join(",", selectedSeconds.OrderBy(s => s));
		var key = (table, anchor, start, selectedKey);
		if (_loadedFor == key)
			return;

		_loading = true;
		try
		{
			var seconds = selectedSeconds.ToList();
			// MeanStdDev, not Ohlc -- see RuleEngineHelper.TxFoldRows for why true high/low can't work here.
			Bars = await Task.Run(() =>
			{
				var ticks = tickRepository.ReadTicks(table, anchor.AddSeconds(-seconds.Max()), anchor).ToList();
				return TxFoldStats.Compute(ticks, anchor, start, seconds, BarAggregationKind.MeanStdDev).Bars;
			});
			_loadedFor = key;
		}
		finally
		{
			_loading = false;
		}
	}
}
