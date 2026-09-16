using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;

namespace Poink.Sim.Components.Shared.BrainIoPanel;

public partial class BrainIoPanel : ComponentBase
{
	[Parameter]
	public IReadOnlyList<RlInputFeature>? Inputs { get; set; }

	[Parameter]
	public IReadOnlyList<RlOutputFeature>? Outputs { get; set; }

	[Parameter]
	public double? CurrentPrice { get; set; }

	[Parameter]
	public double? Equity { get; set; }

	private bool _showDenormalizedInputs;

	private static bool IsPriceWindowLabel(string label) => label.StartsWith("t-", System.StringComparison.Ordinal);

	private static bool IsVolumeProfileLabel(string label) =>
		label.StartsWith("vp-", System.StringComparison.Ordinal)
		|| label.StartsWith("vpr-", System.StringComparison.Ordinal)
		|| label is "poc-offset" or "va-high-offset" or "va-low-offset";

	private static bool IsTimeProfileLabel(string label) =>
		label.StartsWith("tp-", System.StringComparison.Ordinal)
		|| label.StartsWith("tpr-", System.StringComparison.Ordinal);

	private static bool IsXGridLabel(string label) => label.StartsWith("xgrid-", System.StringComparison.Ordinal);

	private static string XGridKey(string label) => label["xgrid-".Length..];

	// Catalog lookup, keyed the same way the Brain Builder's own XGrid signal list is (see
	// XGridSignalCatalog.All), so the input view's grouping/labels/colours stay in lockstep with it.
	private static readonly Dictionary<string, XGridSignalDef> XGridDefsByKey =
		XGridSignalCatalog.All.ToDictionary(d => d.Key);

	private static readonly Dictionary<string, int> XGridOrderByKey =
		XGridSignalCatalog.All.Select((d, i) => (d.Key, i)).ToDictionary(x => x.Key, x => x.i);

	private static string XGridGroup(string label) =>
		XGridDefsByKey.TryGetValue(XGridKey(label), out var def) ? def.Group : "Other";

	private static string XGridDisplayLabel(string label) =>
		XGridDefsByKey.TryGetValue(XGridKey(label), out var def) ? def.Label : label;

	private static string XGridTooltip(string label) =>
		XGridDefsByKey.TryGetValue(XGridKey(label), out var def) ? def.Tooltip : label;

	private static string XGridColorVar(string label) =>
		XGridDefsByKey.TryGetValue(XGridKey(label), out var def) ? def.ColorVar : "--color-secondary";

	private static bool IsPriceLevelLabel(string label) =>
		label.Contains("price", System.StringComparison.Ordinal)
		|| label.Contains("poc", System.StringComparison.Ordinal)
		|| label.Contains("va-high", System.StringComparison.Ordinal)
		|| label.Contains("va-low", System.StringComparison.Ordinal);

	private List<RlInputFeature> PriceWindowFeatures =>
		Inputs?.Where(f => IsPriceWindowLabel(f.Label)).ToList() ?? new List<RlInputFeature>();

	private List<RlInputFeature> VolumeProfilePriceFeatures =>
		Inputs?.Where(f => IsVolumeProfileLabel(f.Label) && IsPriceLevelLabel(f.Label)).ToList()
		?? new List<RlInputFeature>();

	private List<RlInputFeature> VolumeProfileVolumeFeatures =>
		Inputs?.Where(f => IsVolumeProfileLabel(f.Label) && !IsPriceLevelLabel(f.Label)).ToList()
		?? new List<RlInputFeature>();

	private List<RlInputFeature> TimeProfilePriceFeatures =>
		Inputs?.Where(f => IsTimeProfileLabel(f.Label) && IsPriceLevelLabel(f.Label)).ToList()
		?? new List<RlInputFeature>();

	private List<RlInputFeature> TimeProfileTimeFeatures =>
		Inputs?.Where(f => IsTimeProfileLabel(f.Label) && !IsPriceLevelLabel(f.Label)).ToList()
		?? new List<RlInputFeature>();

	private List<RlInputFeature> XGridFeatures =>
		Inputs
			?.Where(f => IsXGridLabel(f.Label))
			.OrderBy(f => XGridOrderByKey.TryGetValue(XGridKey(f.Label), out var index) ? index : int.MaxValue)
			.ToList()
		?? new List<RlInputFeature>();

	private IEnumerable<IGrouping<string, RlInputFeature>> XGridFeatureGroups =>
		XGridFeatures.GroupBy(f => XGridGroup(f.Label));

	private List<RlInputFeature> TraderStateFeatures =>
		Inputs
			?.Where(f =>
				!IsPriceWindowLabel(f.Label)
				&& !IsVolumeProfileLabel(f.Label)
				&& !IsTimeProfileLabel(f.Label)
				&& !IsXGridLabel(f.Label))
			.ToList()
		?? new List<RlInputFeature>();

	private IEnumerable<IGrouping<string, RlOutputFeature>> OutputGroups =>
		Outputs?.GroupBy(o => o.Group) ?? Enumerable.Empty<IGrouping<string, RlOutputFeature>>();

	private string FormatInputValue(RlInputFeature f, string normalizedFormat = "N4")
	{
		if (!_showDenormalizedInputs)
			return f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture);

		var anchors = new DenormalizationAnchors(CurrentPrice, Equity);
		return f.Normalizer.Denormalize((float)f.Value, anchors) is { } raw
			? f.Normalizer.Format(raw)
			: f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture);
	}

	private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

	private static string FormatProbabilityPct(double probability) =>
		(probability * 100).ToString("N1", CultureInfo.InvariantCulture);

	private static string FormatLogit(double logit) => logit.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

	private static string ActionShortLabel(RlOutputFeature o) =>
		o.Group == "Direction" ? ActionShortLabel(o.Label) : o.Label;

	private static string ActionCssSuffix(RlOutputFeature o) =>
		o.Group == "Direction" ? ActionCssSuffix(o.Label) : "level";

	private static string ActionShortLabel(string actionLabel) =>
		actionLabel switch
		{
			"Hold" => "HOLD",
			"EnterLong" => "LONG",
			"EnterShort" => "SHORT",
			"Close" => "CLOSE",
			_ => actionLabel,
		};

	private static string ActionCssSuffix(string actionLabel) =>
		actionLabel switch
		{
			"Hold" => "hold",
			"EnterLong" => "long",
			"EnterShort" => "short",
			"Close" => "close",
			_ => "",
		};
}
