// ── Normalizer: raw quantity <-> network-ready feature value ────────────
//  Split out of each IObservationEncoder's own Encode() -- an encoder
//  decides WHICH raw quantity feeds a given feature slot, a normalizer
//  decides HOW that quantity is scaled into the bounded, scale-free value
//  the network actually sees. Lets a new encoder reuse an existing
//  normalizer (or a new normalizer slot into an existing encoder) without
//  either side needing to know about the other's internals.
//
//  Normalize is encode-time and always has a live SimulationObservation to
//  pull its anchor from (CurrentPrice, Capital.Equity, ...). Denormalize is
//  display-time (TrainLiveTab's de-normalized INPUTS view) and only has
//  DenormalizationAnchors -- a historical replay bar's derived
//  price/equity, not a live observation -- so it takes that instead; it
//  returns null when the anchor it needs isn't available yet, and the
//  caller falls back to the plain normalized value rather than each
//  implementation duplicating that fallback. Format turns an already-
//  denormalized raw value into its display string, kept separate from
//  Denormalize for the same reason.
// ─────────────────────────────────────────────────────────────────────────
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

// The real-world reference points TrainLiveTab's episode replay has on hand
// for denormalizing a historical bar's feature values -- CurrentPrice/Equity
// from a live SimulationObservation, standing in for one. Null when the
// anchor a given feature needs isn't available yet (e.g. no equity captured
// because the episode's brain isn't describable).
internal readonly record struct DenormalizationAnchors(double? CurrentPrice, double? Equity);

internal interface IFeatureNormalizer
{
    float Normalize(double rawValue, SimulationObservation observation);

    double? Denormalize(float normalizedValue, DenormalizationAnchors anchors);

    string Format(double rawValue);
}
