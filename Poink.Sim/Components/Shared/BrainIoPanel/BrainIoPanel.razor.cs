using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Components.Shared.BrainIoPanel;

// Shared by TrainLiveTab (Train page) and SimulationPage's INPUTS/OUTPUTS tab
// -- "what the network sees/decided" for one decision, generic across either
// page's own notion of "which decision" (Train's live episode/frozen replay,
// Simulation's selected trader's last live decision). Callers resolve that
// down to plain Inputs/Outputs/CurrentPrice/Equity before handing them in --
// this component itself doesn't know about episodes, replays, or trader
// sessions.
public partial class BrainIoPanel : ComponentBase
{
    [Parameter] public IReadOnlyList<RlInputFeature>? Inputs { get; set; }
    [Parameter] public IReadOnlyList<RlOutputFeature>? Outputs { get; set; }
    [Parameter] public double? CurrentPrice { get; set; }
    [Parameter] public double? Equity { get; set; }

    // Toggles the INPUTS card between the network's own normalized (bounded
    // ratio) values and their de-normalized real-world units.
    private bool _showDenormalizedInputs;

    // Every encoder's label convention this panel groups inputs by -- see
    // BarWindowEncoder ("t-N", same in both its Normalized and Raw modes),
    // VolumeProfileEncoder ("vp-"/"vpr-" bucket labels depending on its
    // Normalized/Raw pick, plus the Normalized mode's unprefixed
    // "poc-offset"/"va-high-offset"/"va-low-offset" summary scalars), and
    // TimeProfileEncoder ("tp-"/"tpr-", same split). Splitting on these
    // conventions rather than a fixed count keeps this working if a future
    // brain's window/bucket sizes differ. Whatever matches none of them is
    // TraderStateEncoder's plain-named account-state scalars.
    private static bool IsPriceWindowLabel(string label) => label.StartsWith("t-", System.StringComparison.Ordinal);

    private static bool IsVolumeProfileLabel(string label) =>
        label.StartsWith("vp-", System.StringComparison.Ordinal) || label.StartsWith("vpr-", System.StringComparison.Ordinal) ||
        label is "poc-offset" or "va-high-offset" or "va-low-offset";

    private static bool IsTimeProfileLabel(string label) =>
        label.StartsWith("tp-", System.StringComparison.Ordinal) || label.StartsWith("tpr-", System.StringComparison.Ordinal);

    // Within a profile's own group, its POC/value-area summary and (raw
    // variant only) each bucket's exact price form the "price" half; the
    // histogram fractions / raw tick-counts or seconds form the other half
    // (volume for Volume Profile, time for Time Profile).
    private static bool IsPriceLevelLabel(string label) =>
        label.Contains("price", System.StringComparison.Ordinal) || label.Contains("poc", System.StringComparison.Ordinal) ||
        label.Contains("va-high", System.StringComparison.Ordinal) || label.Contains("va-low", System.StringComparison.Ordinal);

    private List<RlInputFeature> PriceWindowFeatures =>
        Inputs?.Where(f => IsPriceWindowLabel(f.Label)).ToList() ?? new List<RlInputFeature>();

    private List<RlInputFeature> VolumeProfilePriceFeatures =>
        Inputs?.Where(f => IsVolumeProfileLabel(f.Label) && IsPriceLevelLabel(f.Label)).ToList() ?? new List<RlInputFeature>();

    private List<RlInputFeature> VolumeProfileVolumeFeatures =>
        Inputs?.Where(f => IsVolumeProfileLabel(f.Label) && !IsPriceLevelLabel(f.Label)).ToList() ?? new List<RlInputFeature>();

    private List<RlInputFeature> TimeProfilePriceFeatures =>
        Inputs?.Where(f => IsTimeProfileLabel(f.Label) && IsPriceLevelLabel(f.Label)).ToList() ?? new List<RlInputFeature>();

    private List<RlInputFeature> TimeProfileTimeFeatures =>
        Inputs?.Where(f => IsTimeProfileLabel(f.Label) && !IsPriceLevelLabel(f.Label)).ToList() ?? new List<RlInputFeature>();

    private List<RlInputFeature> TraderStateFeatures =>
        Inputs?.Where(f => !IsPriceWindowLabel(f.Label) && !IsVolumeProfileLabel(f.Label) && !IsTimeProfileLabel(f.Label)).ToList()
        ?? new List<RlInputFeature>();

    // One bar-set per RlOutputFeature.Group -- "Direction" (the fixed
    // Hold/EnterLong/EnterShort/Close pick) plus one group per
    // IActionOutputHead the brain was built with (e.g. "Volume"). Each
    // group's own probabilities already sum to ~1 independently, so they're
    // rendered as separate 100%-wide bar-sets rather than one flat list.
    private IEnumerable<IGrouping<string, RlOutputFeature>> OutputGroups =>
        Outputs?.GroupBy(o => o.Group) ?? Enumerable.Empty<IGrouping<string, RlOutputFeature>>();

    // Converts one feature's normalized value into a human string, via
    // whichever IFeatureNormalizer produced it (see
    // IObservationEncoder.Features/RlInputFeature.Normalizer) -- generic
    // across every encoder's features, so a new encoder/normalizer never
    // needs a matching new case here. Falls back to the plain normalized
    // value if the anchor a feature's normalizer needs isn't available yet
    // (e.g. no equity captured because the brain isn't describable).
    private string FormatInputValue(RlInputFeature f, string normalizedFormat = "N4")
    {
        if (!_showDenormalizedInputs) return f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture);

        var anchors = new DenormalizationAnchors(CurrentPrice, Equity);
        return f.Normalizer.Denormalize((float)f.Value, anchors) is { } raw
            ? f.Normalizer.Format(raw)
            : f.Value.ToString(normalizedFormat, CultureInfo.InvariantCulture);
    }

    // CSS width percentages must use '.' as the decimal separator regardless
    // of host locale (this host's is comma-decimal) -- see TickVolumeProfileChart
    // for the same concern.
    private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // One decimal place (unlike Pct's variable precision) -- probabilities
    // are always in [0,100] and this reads better lined up in the OUTPUTS
    // card's fixed-width column than Pct's "0.##" would.
    private static string FormatProbabilityPct(double probability) => (probability * 100).ToString("N1", CultureInfo.InvariantCulture);

    // Signed (unlike FormatProbabilityPct) since a logit is unbounded and its
    // sign is meaningful on its own, unlike a probability.
    private static string FormatLogit(double logit) => logit.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

    // Maps an RlOutputFeature.Label (a SimulationAction member name) onto a
    // short display label and CSS suffix -- only meaningful for the
    // "Direction" group; every other group (Volume/StopLoss/TakeProfit)
    // shows its level label as-is with a neutral "level" style, since those
    // aren't SimulationAction names.
    private static string ActionShortLabel(RlOutputFeature o) => o.Group == "Direction" ? ActionShortLabel(o.Label) : o.Label;

    private static string ActionCssSuffix(RlOutputFeature o) => o.Group == "Direction" ? ActionCssSuffix(o.Label) : "level";

    private static string ActionShortLabel(string actionLabel) => actionLabel switch
    {
        "Hold" => "HOLD",
        "EnterLong" => "LONG",
        "EnterShort" => "SHORT",
        "Close" => "CLOSE",
        _ => actionLabel,
    };

    private static string ActionCssSuffix(string actionLabel) => actionLabel switch
    {
        "Hold" => "hold",
        "EnterLong" => "long",
        "EnterShort" => "short",
        "Close" => "close",
        _ => "",
    };
}
