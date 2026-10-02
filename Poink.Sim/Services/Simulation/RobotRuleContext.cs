using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Simulation;

/// <summary>The bar-derived rule-engine inputs (merged volume profile and XGrid) that stay the same until the next bar completes.</summary>
internal sealed class RobotBarData
{
	public required IReadOnlyList<WeightedPriceSample> Samples { get; init; }
	public required IReadOnlyList<HlocAvdtBar> Bars { get; init; }
	public required XGridResult? Grid { get; init; }
	public required double PipSize { get; init; }
	public required int Window { get; init; }

	public static RobotBarData? Create(
		IReadOnlyList<HlocAvdtBar> recentBars,
		IReadOnlyList<BarPriceHistogram> recentHistograms,
		double pipSize,
		RobotOptions options,
		bool includeGrid
	)
	{
		int window = Math.Clamp(options.BarWindow, 1, Math.Max(1, recentBars.Count));
		int offset = Math.Min(Math.Max(0, options.BarOffset), Math.Max(0, recentBars.Count - 1));
		int count = recentBars.Count - offset;
		if (count <= 0)
			return null;

		var bars = recentBars.Take(count).ToList();
		var histograms = recentHistograms.Take(count).ToList();
		var samples = BarPriceHistogram
			.Merge(histograms, window)
			.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
			.ToList();

		int gridWindow = Math.Min(window, bars.Count);
		var grid = includeGrid
			? XGridStats.Compute(
				bars,
				histograms,
				samples,
				bars[bars.Count - gridWindow].OpenTime,
				bars[^1].OpenTime,
				options.XGridPips * pipSize,
				options.XGridCols
			)
			: null;
		return new RobotBarData
		{
			Samples = samples,
			Bars = bars,
			Grid = grid,
			PipSize = pipSize,
			Window = window,
		};
	}
}

/// <summary>
/// The rule-engine signals at one price, asked whether each of the robot's rule sets matches -- the
/// engine-side counterpart of what the Simulation page's rule panels show. Market rows are built once per rule
/// set and shared by every bot; position rows are added per bot, from that bot's own open position.
/// </summary>
internal sealed class RobotRuleContext
{
	private readonly RobotBarData _barData;
	private readonly IReadOnlyList<TxFoldBar> _txFoldBars;
	private readonly double _currentPrice;
	private readonly RuleSignalMarket _market;
	private readonly Dictionary<RobotRules, IReadOnlyList<RuleSignalRow>> _marketRows = new(ReferenceEqualityComparer.Instance);

	public RobotRuleContext(RobotBarData barData, double currentPrice, IReadOnlyList<TxFoldBar> txFoldBars, RuleSignalMarket market)
	{
		_barData = barData;
		_currentPrice = currentPrice;
		_txFoldBars = txFoldBars;
		_market = market;
	}

	public bool Matches(RobotRules rules, Position? position = null)
	{
		if (rules.EnabledKeys.Count == 0)
			return false;

		var rows = MarketRowsFor(rules);
		if (UsesPositionRows(rules))
			rows = rows.Concat(
					RuleSignalRowsHelper.PositionRows(
						position,
						_market.Bid,
						_market.Ask,
						_market.TimestampUtc,
						_market.BarSize,
						_market.Settings
					)
				)
				.ToList();
		return RuleEngineHelper.Matches(rows, rules.EnabledKeys, rules.MinMatches);
	}

	private static bool UsesPositionRows(RobotRules rules) =>
		!rules.DisabledGroups.Contains(RuleSignalGroup.Position)
		&& rules.EnabledKeys.Any(key => RuleEngineHelper.GroupOf(key) == RuleSignalGroup.Position);

	private IReadOnlyList<RuleSignalRow> MarketRowsFor(RobotRules rules)
	{
		if (_marketRows.TryGetValue(rules, out var cached))
			return cached;

		bool baseline = !rules.DisabledGroups.Contains(RuleSignalGroup.Baseline);
		var result = RuleEngineHelper.Compute(
			_barData.Samples,
			w => w.Price,
			w => (double)w.Count,
			_barData.Bars,
			b => b.Average,
			b => (double)b.Volume,
			b => b.Close,
			_currentPrice,
			rules.DisabledGroups.Contains(RuleSignalGroup.XGrid) ? null : _barData.Grid,
			rules.XGridPercent,
			baseline,
			rules.InTolerancePips * _barData.PipSize,
			_market,
			rules.DisabledGroups
		);
		var txFoldRows = rules.DisabledGroups.Contains(RuleSignalGroup.TxFold)
			? Array.Empty<RuleSignalRow>()
			: RuleEngineHelper.TxFoldRows(_txFoldBars, _currentPrice);
		var rows = result.Rows.Concat(txFoldRows).ToList();
		_marketRows[rules] = rows;
		return rows;
	}
}
