// ── Composable brain capability: observation -> feature vector ──────────
//  Sibling to IPolicyGradientBrain/IHasLookbackBars, same "narrow interface,
//  checked via is" house style -- this is the half of a brain that used to
//  be baked directly into each concrete brain's own Encode method (see e.g.
//  the now-deleted MlpPolicyBrain/VolumeProfilePolicyBrain). Pulling it out
//  lets ComposedPolicyBrain (see that file) pair any encoder with any
//  INetworkArchitecture instead of one hardwired combination per class.
//
//  Encode returns a plain float[], not a Tensor -- an encoder never touches
//  TorchSharp/autograd/device placement at all; ComposedPolicyBrain is the
//  one place that wraps the result into a Tensor and moves it to whatever
//  device its Network currently lives on.
//
//  SequenceLength is the one piece of contract that couples an encoder to a
//  recurrent architecture: 0 means "no natural sequence order", otherwise
//  it's the width of a LEADING slice of Encode's output that a recurrent
//  architecture (see LstmArchitecture) can narrow() out and feed through an
//  LSTM as a true time series, with the remaining trailing values treated as
//  plain scalars. CompositeEncoder (see that file) sums this across its
//  parts and enforces that only the first part may be sequence-bearing --
//  every implementer of this interface should be written with that ordering
//  assumption in mind if it reports SequenceLength > 0.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

// One feature slot's fixed label paired with the IFeatureNormalizer that
// produced (and can invert) its value -- see that interface's doc comment.
// Carrying the normalizer alongside the label (rather than a separate
// parallel list) is what lets ComposedPolicyBrain.DescribeInputs build an
// RlInputFeature that TrainLiveTab can de-normalize generically, without
// knowing any encoder's label conventions by name.
internal readonly record struct FeatureDescriptor(string Label, IFeatureNormalizer Normalizer);

internal interface IObservationEncoder
{
    int FeatureCount { get; }

    int SequenceLength { get; }

    // How many trailing bars of *real* history this encoder needs before its
    // output reflects genuine data rather than a padded/undersized window
    // (e.g. BarWindowEncoder's WindowSize, or VolumeProfileEncoder's own
    // LookbackBars) -- 0 for an encoder with no windowing (e.g.
    // CapitalStateEncoder). RlEpisodeRunner reads this (via ComposedPolicyBrain/
    // CompositeEncoder) to run a training episode's first WarmupBars completed
    // bars purely to fill recentBars/recentBarHistograms before any decision
    // is recorded, instead of ever exercising an encoder's own padding
    // fallback during training.
    int WarmupBars { get; }

    // Fixed, observation-independent label + normalizer for each of
    // FeatureCount values -- used to describe Encode's output (see
    // ComposedPolicyBrain.DescribeInputs) without needing to run the
    // network, and to invert a normalized value back to real units for
    // display (see IFeatureNormalizer.Denormalize).
    IReadOnlyList<FeatureDescriptor> Features { get; }

    float[] Encode(SimulationObservation observation);
}
