// ── UI-facing catalog of buildable encoder/architecture pieces ──────────
//  What the Train page's Brain Builder panel (see RlTrainingService.
//  TryBuildBrain / TrainPage.razor.cs) actually offers -- a plain enum-driven
//  factory rather than reflection/attribute scanning, since the set of
//  encoders/architectures is small and changes rarely; adding a new one here
//  plus a new enum case is the whole cost of making it pickable in the UI.
//
//  TraderStateEncoder is deliberately NOT an EncoderKind option -- every
//  built brain needs account state regardless of how its market data is
//  represented, so RlTrainingService.TryBuildBrain appends it unconditionally
//  rather than letting a user omit it.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

// Public (not internal like most of this file) because BrainRecipe below
// exposes both as public properties, and BrainRecipe itself must be public
// since it travels on TrainedModelSummary/NetworkSummary (see those records'
// doc comments on why they're public -- a Razor component's [Parameter]
// property can't be typed with a less-accessible type than the component
// itself).
public enum EncoderKind { BarWindow, VolumeProfile, TimeProfile, SessionClock, SpreadVolatility }

public enum ArchitectureKind { Mlp, Lstm, Cnn }

// How a pickable encoder feeds its raw quantities to the network -- Normalized
// is each encoder's original convention (fraction-of-total / percent-offset-
// from-current-price via FractionNormalizer/PriceOffsetNormalizer), Raw feeds
// the literal quantity (tick count/seconds/price) via RawValueNormalizer/
// RawPriceNormalizer instead. A per-set build option (see EncoderSizeOptions),
// orthogonal to EncoderKind -- picking this never changes which EncoderKind a
// set maps to, only which normalizer pair its encoder is constructed with.
// Deliberately just two members today, but the whole point of splitting this
// out from EncoderKind is that a future strategy (log-returns, z-score, ...
// see TODO/2026-07-26-normalization-strategies.md) is one new member handled
// inside each encoder's existing normalization branch, not a new EncoderKind
// value plus a whole new duplicate encoder class per set.
public enum NormalizationKind { Normalized, Raw }

// Which extra (non-direction) decisions a brain's network makes, on top of
// the fixed Hold/EnterLong/EnterShort/Close pick -- see IActionOutputHead.
// Independently selectable in the Train page's Build Brain panel; each one
// enabled appends its own head to the network's output layer.
public enum ActionOutputKind { Volume, StopLoss, TakeProfit }

// How an enabled ActionOutputKind's raw output slots decode into a value --
// see IActionOutputHead's doc comment. Only one member today; the whole
// point of the interface is that a future Continuous member can be added
// here (plus a new IActionOutputHead implementation and a new case in
// BrainCatalog.CreateActionOutputHead) without touching ComposedPolicyBrain,
// RlTrainingService, or persistence at all.
public enum ActionOutputEncodingKind { DiscretizedLevels }

// How a categorical distribution (the direction pick, and every enabled
// ActionOutputKind's own levels) collapses to one chosen index. Softmax
// samples stochastically, weighted by probability -- today's original
// behavior, and what training needs for exploration. HardMax instead always
// picks the highest-probability (equivalently highest-logit) index, for
// evaluating a deterministic policy against what the network actually
// learned. One brain-wide build-time choice, applied uniformly to the
// direction pick and every action-output head -- see ComposedPolicyBrain.
// DecideWithLogProbability and DiscretizedLevelsActionHead.Sample. Either way
// the chosen index's log-probability/entropy still come from the same
// softmax distribution, so REINFORCE training is unaffected by which mode a
// brain was built with. See BrainRecipe.DecisionTemperature for the dial
// between these two extremes.
public enum DecisionDecodingKind { Softmax, HardMax }

// Every pickable set's build-time options -- structural width (window/bucket
// count) plus, now, its NormalizationKind and (for the two profiles) its
// merge-window length ("Window" in the Brain Builder's Input table). All of
// these change either FeatureCount or the encoder's fixed behavior, so
// they're baked into the encoder once at construction via
// BrainCatalog.CreateEncoder, then carried on BrainRecipe so LoadSavedBrain
// reconstructs the exact same shape a saved snapshot's weights were trained
// with -- nothing here is runtime-adjustable after a brain is built, same
// treatment bucket count always had. VolumeProfileLookbackBars/
// TimeProfileLookbackBars replace what used to be one runtime value shared
// (broadcast) across both profiles via the since-removed IHasLookbackBars --
// each profile now gets its own independent, fixed value instead.
// BarWindowSize/VolumeProfileBucketCount/TimeProfileBucketCount keep their
// original names/positions/defaults so a recipe saved before this record
// gained Normalization/Lookback still deserializes those three correctly by
// name; the two new *LookbackBars properties default to 50 (each encoder's
// own original hardcoded default) for the same old-recipe-tolerance reason.
// SpreadVolatilityAtrPeriod is the newest such property, same treatment:
// defaults to 14 (SpreadVolatilityEncoder's own hardcoded default) so a
// recipe saved before that encoder existed still deserializes.
public sealed record EncoderSizeOptions(
    int BarWindowSize = 20,
    int VolumeProfileBucketCount = 20,
    int TimeProfileBucketCount = 20,
    int VolumeProfileLookbackBars = 50,
    int TimeProfileLookbackBars = 50,
    NormalizationKind BarWindowNormalization = NormalizationKind.Normalized,
    NormalizationKind VolumeProfileNormalization = NormalizationKind.Normalized,
    NormalizationKind TimeProfileNormalization = NormalizationKind.Normalized,
    int SpreadVolatilityAtrPeriod = 14);

// A runtime-built brain's exact shape -- what LoadSavedBrain/TryStartTraining
// need to reconstruct an identical encoder/architecture/action composition
// after a restart wipes it from BrainRegistry (see RlTrainingService.
// TryBuildBrain). Every neural brain is built through TryBuildBrain and
// persisted as a NetworkService network under its own name now -- there are
// no hardcoded brains that skip this.
// ActionOutputKinds defaults to null (equivalent to empty -- no extra heads)
// so a brain built before Volume/StopLoss/TakeProfit existed still
// deserializes; ActionOutputEncodingKind defaults to DiscretizedLevels,
// today's only implementation. EnabledDirections defaults to null
// (equivalent to ComposedPolicyBrain.AllDirections -- every one of
// Hold/EnterLong/EnterShort/Close) for the same reason, and for the same
// reason: every direction is individually optional per brain, not just the
// added heads (see SimulationAction's doc comment). EncoderSizes defaults to
// null (equivalent to EncoderSizeOptions' own defaults -- a record type
// can't itself be a default parameter value, only null can), same
// deserialization-tolerance reasoning as the other optional properties here.
// DecisionDecoding defaults to Softmax, today's original (and only, before
// HardMax existed) behavior, for the same old-recipe-tolerance reason.
// DecisionTemperature defaults to 1.0 (today's original, unscaled softmax),
// same reasoning -- only meaningful under Softmax (dividing HardMax's logits
// by a positive temperature before taking argmax can't change which index is
// largest, so ComposedPolicyBrain/DiscretizedLevelsActionHead skip it under
// HardMax); values below 1 sharpen the distribution toward its argmax
// (fewer, more confident choices explored), above 1 flatten it toward
// uniform (more exploration) -- a dial between Softmax's and HardMax's two
// extremes rather than a third decoding mode, so it composes with either.
public sealed record BrainRecipe(
    IReadOnlyList<EncoderKind> EncoderKinds, ArchitectureKind ArchitectureKind, IReadOnlyList<int> HiddenSizes,
    IReadOnlyList<ActionOutputKind>? ActionOutputKinds = null,
    ActionOutputEncodingKind ActionOutputEncodingKind = ActionOutputEncodingKind.DiscretizedLevels,
    IReadOnlyList<SimulationAction>? EnabledDirections = null,
    EncoderSizeOptions? EncoderSizes = null,
    DecisionDecodingKind DecisionDecoding = DecisionDecodingKind.Softmax,
    double DecisionTemperature = 1.0)
{
    public override string ToString()
    {
        string baseDescription = $"{ArchitectureKind}[{string.Join(",", HiddenSizes)}] {string.Join("+", EncoderKinds)}";
        if (EnabledDirections is { Count: > 0 } directions)
            baseDescription += $" [{string.Join(",", directions)}]";
        if (ActionOutputKinds is { Count: > 0 })
            baseDescription += $" +{string.Join("+", ActionOutputKinds)}";
        if (DecisionDecoding == DecisionDecodingKind.HardMax)
            baseDescription += " [HardMax]";
        else if (DecisionTemperature != 1.0)
            baseDescription += $" [T={DecisionTemperature:0.##}]";
        return baseDescription;
    }
}

internal static class BrainCatalog
{
    public static IObservationEncoder CreateEncoder(EncoderKind kind, EncoderSizeOptions? sizeOptions = null)
    {
        var sizes = sizeOptions ?? new EncoderSizeOptions();
        return kind switch
        {
            EncoderKind.BarWindow => new BarWindowEncoder(windowSize: sizes.BarWindowSize, normalization: sizes.BarWindowNormalization),
            EncoderKind.VolumeProfile => new VolumeProfileEncoder(
                bucketCount: sizes.VolumeProfileBucketCount, lookbackBars: sizes.VolumeProfileLookbackBars, normalization: sizes.VolumeProfileNormalization),
            EncoderKind.TimeProfile => new TimeProfileEncoder(
                bucketCount: sizes.TimeProfileBucketCount, lookbackBars: sizes.TimeProfileLookbackBars, normalization: sizes.TimeProfileNormalization),
            EncoderKind.SessionClock => new SessionClockEncoder(),
            EncoderKind.SpreadVolatility => new SpreadVolatilityEncoder(atrPeriod: sizes.SpreadVolatilityAtrPeriod),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // hiddenSizes is the MLP's variable-length hidden-layer list for Mlp,
    // exactly [hiddenSize, numLayers] for Lstm, or [kernelSize, channel1,
    // channel2, ...] for Cnn -- the Train page builds this list in whichever
    // shape matches the architecture the user picked.
    public static INetworkArchitecture CreateArchitecture(ArchitectureKind kind, IReadOnlyList<int> hiddenSizes) => kind switch
    {
        ArchitectureKind.Mlp => new MlpArchitecture([.. hiddenSizes]),
        ArchitectureKind.Lstm => new LstmArchitecture(
            hiddenSize: hiddenSizes.Count > 0 ? hiddenSizes[0] : 64,
            numLayers: hiddenSizes.Count > 1 ? hiddenSizes[1] : 2),
        ArchitectureKind.Cnn => new CnnArchitecture(
            kernelSize: hiddenSizes.Count > 0 ? hiddenSizes[0] : 3,
            channels: hiddenSizes.Count > 1 ? [.. hiddenSizes.Skip(1)] : [16, 32]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static IActionOutputHead CreateActionOutputHead(
        ActionOutputKind kind, ActionOutputEncodingKind encodingKind,
        DecisionDecodingKind decoding = DecisionDecodingKind.Softmax, double temperature = 1.0) => encodingKind switch
    {
        ActionOutputEncodingKind.DiscretizedLevels => new DiscretizedLevelsActionHead(kind, decoding, temperature),
        _ => throw new ArgumentOutOfRangeException(nameof(encodingKind), encodingKind, null),
    };
}
