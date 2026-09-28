using System.Collections.Generic;

namespace Poink.Sim.Components.Charts;

// Public (not internal) since TxFold's [Parameter] ShownLevels exposes it, same rationale as BarAggregationKind.
public enum TxFoldLevelKind
{
	Poc,
	Vah,
	Val,
	Vwap,
	Average,
	Median,
	Regression,
}

internal readonly record struct TxFoldLevelOption(TxFoldLevelKind Kind, string Label, string Tooltip);

internal static class TxFoldLevelOptions
{
	public static readonly IReadOnlyList<TxFoldLevelOption> All = new[]
	{
		new TxFoldLevelOption(TxFoldLevelKind.Poc, "POC", "Point of Control -- the price with the most tick activity across the ticks behind the displayed bars"),
		new TxFoldLevelOption(TxFoldLevelKind.Vah, "VAH", "Value Area High -- the upper edge of the price band holding 70% of tick activity"),
		new TxFoldLevelOption(TxFoldLevelKind.Val, "VAL", "Value Area Low -- the lower edge of the price band holding 70% of tick activity"),
		new TxFoldLevelOption(TxFoldLevelKind.Vwap, "VWAP", "Volume-weighted average price -- ticks carry no volume of their own, so this equals AVE unless that changes"),
		new TxFoldLevelOption(TxFoldLevelKind.Average, "AVE", "Mean tick price across the ticks behind the displayed bars"),
		new TxFoldLevelOption(TxFoldLevelKind.Median, "MED", "Median tick price across the ticks behind the displayed bars"),
		new TxFoldLevelOption(TxFoldLevelKind.Regression, "REG", "Linear regression fit of tick price over time, evaluated at the most recent tick"),
	};
}
