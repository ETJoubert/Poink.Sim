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
		};
	}
}

/// <summary>
/// The rule-engine signals at one price, asked whether each of the robot's rule sets matches -- the
/// engine-side counterpart of what the Simulation page's rule panels show.
/// </summary>
internal sealed class RobotRuleContext
{
	private readonly RobotBarData _barData;
	private readonly IReadOnlyList<TxFoldBar> _txFoldBars;
	private readonly double _currentPrice;
	private readonly Dictionary<RobotRules, bool> _matches = new(ReferenceEqualityComparer.Instance);

	public RobotRuleContext(RobotBarData barData, double currentPrice, IReadOnlyList<TxFoldBar> txFoldBars)
	{
		_barData = barData;
		_currentPrice = currentPrice;
		_txFoldBars = txFoldBars;
	}

	/// <summary>Answers once per rule set; every bot asking the same context with the same rules gets the cached answer.</summary>
	public bool Matches(RobotRules rules)
	{
		if (!_matches.TryGetValue(rules, out bool matches))
		{
			matches = Evaluate(rules);
			_matches[rules] = matches;
		}
		return matches;
	}

	private bool Evaluate(RobotRules rules)
	{
		if (rules.EnabledKeys.Count == 0)
			return false;

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
			rules.InTolerancePips * _barData.PipSize
		);
		var txFoldRows = rules.DisabledGroups.Contains(RuleSignalGroup.TxFold)
			? Array.Empty<RuleSignalRow>()
			: RuleEngineHelper.TxFoldRows(_txFoldBars, _currentPrice);
		var rows = result.Rows.Concat(txFoldRows).ToList();
		return RuleEngineHelper.Matches(rows, rules.EnabledKeys, rules.MinMatches);
	}
}
