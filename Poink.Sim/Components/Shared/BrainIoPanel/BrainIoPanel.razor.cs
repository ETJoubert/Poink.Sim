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

    // DescribeInputs labels the price-window entries "t-N" and the
    // account-state scalars by name -- split on that convention rather than a
    // fixed count, so this keeps working if a future brain's window size
    // differs.
    private List<RlInputFeature> PriceWindowFeatures =>
        Inputs?.Where(f => f.Label.StartsWith("t-", System.StringComparison.Ordinal)).ToList() ?? new List<RlInputFeature>();

    private List<RlInputFeature> ScalarFeatures =>
        Inputs?.Where(f => !f.Label.StartsWith("t-", System.StringComparison.Ordinal)).ToList() ?? new List<RlInputFeature>();

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

    // Maps an RlOutputFeature.Label (a SimulationAction member name, see
    // ComposedPolicyBrain.ActionLabels) onto a short display label and CSS
    // suffix.
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
