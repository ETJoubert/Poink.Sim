namespace Poink.Sim.Components.Charts;

// Maps XGridSignalCatalog keys to the CSS colour variable their signal uses in XGrid's own
// live panel, so other UI listing these signals can mirror the same colours.
internal static class XGridSignalColors
{
	public static string Var(string key) =>
		key switch
		{
			XGridSignalCatalog.Keys.RankTopPercentUp or "up-at-poc" or "up-above-poc" or "up-below-poc" =>
				"--color-success",
			XGridSignalCatalog.Keys.RankTopPercentDown or "down-at-poc" or "down-above-poc" or "down-below-poc" =>
				"--color-error",
			XGridSignalCatalog.Keys.AtPoc
			or "above-poc"
			or XGridSignalCatalog.Keys.BelowPoc
			or "total-at-poc"
			or "total-above-poc"
			or "total-below-poc" => "--color-primary",
			XGridSignalCatalog.Keys.AtVah
			or "above-vah"
			or "below-vah"
			or XGridSignalCatalog.Keys.AtVal
			or "above-val"
			or "below-val" => "--color-highlight",
			XGridSignalCatalog.Keys.AtVwap
			or "above-vwap"
			or "below-vwap"
			or "vwap-at-poc"
			or "vwap-above-poc"
			or "vwap-below-poc" => "--color-tertiary",
			"at-low" or "above-low" or "below-low" => "--color-badge-4",
			"at-high" or "above-high" or "below-high" => "--color-badge-5",
			_ => "--color-secondary",
		};
}
