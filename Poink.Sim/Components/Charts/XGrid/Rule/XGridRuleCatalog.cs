using System.Collections.Generic;

namespace Poink.Sim.Components.Charts;

internal readonly record struct XGridRuleRow(
	string Label,
	string ColorVar,
	string? AboveKey,
	string? InKey,
	string? BelowKey
);

// Regroups XGridSignalCatalog's flat entries into Above/In/Below triads for the Rule Engine table.
internal static class XGridRuleCatalog
{
	public static readonly IReadOnlyList<XGridRuleRow> Rows = new[]
	{
		Row("POC", "above-poc", XGridSignalCatalog.Keys.AtPoc, XGridSignalCatalog.Keys.BelowPoc),
		Row("VAH", "above-vah", XGridSignalCatalog.Keys.AtVah, "below-vah"),
		Row("VAL", "above-val", XGridSignalCatalog.Keys.AtVal, "below-val"),
		Row("VWAP", "above-vwap", XGridSignalCatalog.Keys.AtVwap, "below-vwap"),
		Row("Low", "above-low", "at-low", "below-low"),
		Row("High", "above-high", "at-high", "below-high"),
		Row("VWAP/POC", "vwap-above-poc", "vwap-at-poc", "vwap-below-poc"),
		Row("Total/POC", "total-above-poc", "total-at-poc", "total-below-poc"),
		Row("Up/POC", "up-above-poc", "up-at-poc", "up-below-poc"),
		Row("Down/POC", "down-above-poc", "down-at-poc", "down-below-poc"),
		Row("Top % Total", null, "rank-top-total", null),
		Row("Top % Up", null, XGridSignalCatalog.Keys.RankTopPercentUp, null),
		Row("Top % Down", null, XGridSignalCatalog.Keys.RankTopPercentDown, null),
	};

	private static XGridRuleRow Row(string label, string? aboveKey, string? inKey, string? belowKey) =>
		new(label, XGridSignalColors.Var(inKey ?? aboveKey ?? belowKey!), aboveKey, inKey, belowKey);
}
