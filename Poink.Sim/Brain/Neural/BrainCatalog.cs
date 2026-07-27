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
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

// Public (not internal like most of this file) because BrainRecipe below
// exposes both as public properties, and BrainRecipe itself must be public
// since it travels on SavedBrainSummary (see that record's doc comment on
// why it's public -- a Razor component's [Parameter] property can't be typed
// with a less-accessible type than the component itself).
public enum EncoderKind { BarWindow, VolumeProfile, TimeProfile, VolumeProfileRaw, TimeProfileRaw }

public enum ArchitectureKind { Mlp, Lstm }

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

// Per-encoder input width, independent of each other and of IHasLookbackBars'
// LookbackBars (a runtime-mutable merge window that never changes FeatureCount
// -- see that interface's doc comment). These do change FeatureCount, so
// they're build-time-only: baked into the encoder at construction via
// BrainCatalog.CreateEncoder, then carried on BrainRecipe so LoadSavedBrain
// reconstructs the exact same shape a saved snapshot's weights were trained
// with. Defaults match this codebase's original hardcoded bucket/window
// counts, so a recipe saved before this record existed (missing the property
// entirely) still deserializes to the same shape it was originally built with.
public sealed record EncoderSizeOptions(
    int BarWindowSize = 20,
    int VolumeProfileBucketCount = 20,
    int TimeProfileBucketCount = 20,
    int VolumeProfileRawBucketCount = 20,
    int TimeProfileRawBucketCount = 20);

// A runtime-built brain's exact shape -- what LoadSavedBrain needs to
// reconstruct an identical encoder/architecture/action composition after a
// restart wipes it from BrainRegistry (see RlTrainingService.TryBuildBrain).
// Null on a SavedBrainSummary for anything not built through TryBuildBrain --
// the four Program.cs-fixed brains reconstruct via their own source code
// every restart instead, so they never need one.
// ActionOutputKinds defaults to null (equivalent to empty -- no extra heads)
// so a brain built before Volume/StopLoss/TakeProfit existed still
// deserializes; ActionOutputEncodingKind defaults to DiscretizedLevels,
// today's only implementation. EnabledDirections defaults to null
// (equivalent to ComposedPolicyBrain.AllDirections -- every one of
// Hold/EnterLong/EnterShort/Close) for the same reason, and for the same
// reason: every direction is individually optional per brain, not just the
// added heads (see SimulationAction's doc comment). EncoderSizes defaults to
// null (equivalent to EncoderSizeOptions' own all-20 default -- a record type
// can't itself be a default parameter value, only null can), same
// deserialization-tolerance reasoning as the other optional properties here.
public sealed record BrainRecipe(
    IReadOnlyList<EncoderKind> EncoderKinds, ArchitectureKind ArchitectureKind, IReadOnlyList<int> HiddenSizes,
    IReadOnlyList<ActionOutputKind>? ActionOutputKinds = null,
    ActionOutputEncodingKind ActionOutputEncodingKind = ActionOutputEncodingKind.DiscretizedLevels,
    IReadOnlyList<SimulationAction>? EnabledDirections = null,
    EncoderSizeOptions? EncoderSizes = null)
{
    public override string ToString()
    {
        string baseDescription = $"{ArchitectureKind}[{string.Join(",", HiddenSizes)}] {string.Join("+", EncoderKinds)}";
        if (EnabledDirections is { Count: > 0 } directions)
            baseDescription += $" [{string.Join(",", directions)}]";
        return ActionOutputKinds is { Count: > 0 }
            ? $"{baseDescription} +{string.Join("+", ActionOutputKinds)}"
            : baseDescription;
    }
}

internal static class BrainCatalog
{
    public static IObservationEncoder CreateEncoder(EncoderKind kind, EncoderSizeOptions? sizeOptions = null)
    {
        var sizes = sizeOptions ?? new EncoderSizeOptions();
        return kind switch
        {
            EncoderKind.BarWindow => new BarWindowEncoder(windowSize: sizes.BarWindowSize),
            EncoderKind.VolumeProfile => new VolumeProfileEncoder(bucketCount: sizes.VolumeProfileBucketCount),
            EncoderKind.TimeProfile => new TimeProfileEncoder(bucketCount: sizes.TimeProfileBucketCount),
            EncoderKind.VolumeProfileRaw => new VolumeProfileRawEncoder(bucketCount: sizes.VolumeProfileRawBucketCount),
            EncoderKind.TimeProfileRaw => new TimeProfileRawEncoder(bucketCount: sizes.TimeProfileRawBucketCount),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // hiddenSizes is the MLP's variable-length hidden-layer list for Mlp, or
    // exactly [hiddenSize, numLayers] for Lstm -- the Train page builds this
    // list in whichever shape matches the architecture the user picked.
    public static INetworkArchitecture CreateArchitecture(ArchitectureKind kind, IReadOnlyList<int> hiddenSizes) => kind switch
    {
        ArchitectureKind.Mlp => new MlpArchitecture([.. hiddenSizes]),
        ArchitectureKind.Lstm => new LstmArchitecture(
            hiddenSize: hiddenSizes.Count > 0 ? hiddenSizes[0] : 64,
            numLayers: hiddenSizes.Count > 1 ? hiddenSizes[1] : 2),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static IActionOutputHead CreateActionOutputHead(ActionOutputKind kind, ActionOutputEncodingKind encodingKind) => encodingKind switch
    {
        ActionOutputEncodingKind.DiscretizedLevels => new DiscretizedLevelsActionHead(kind),
        _ => throw new ArgumentOutOfRangeException(nameof(encodingKind), encodingKind, null),
    };
}
