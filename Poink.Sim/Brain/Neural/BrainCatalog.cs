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

namespace Poink.Sim.Brain.Neural;

// Public (not internal like most of this file) because BrainRecipe below
// exposes both as public properties, and BrainRecipe itself must be public
// since it travels on SavedBrainSummary (see that record's doc comment on
// why it's public -- a Razor component's [Parameter] property can't be typed
// with a less-accessible type than the component itself).
public enum EncoderKind { BarWindow, VolumeProfile, TimeProfile, VolumeProfileRaw, TimeProfileRaw }

public enum ArchitectureKind { Mlp, Lstm }

// A runtime-built brain's exact shape -- what LoadSavedBrain needs to
// reconstruct an identical encoder/architecture composition after a restart
// wipes it from BrainRegistry (see RlTrainingService.TryBuildBrain). Null on
// a SavedBrainSummary for anything not built through TryBuildBrain -- the
// four Program.cs-fixed brains reconstruct via their own source code every
// restart instead, so they never need one.
public sealed record BrainRecipe(IReadOnlyList<EncoderKind> EncoderKinds, ArchitectureKind ArchitectureKind, IReadOnlyList<int> HiddenSizes)
{
    public override string ToString() => $"{ArchitectureKind}[{string.Join(",", HiddenSizes)}] {string.Join("+", EncoderKinds)}";
}

internal static class BrainCatalog
{
    public static IObservationEncoder CreateEncoder(EncoderKind kind) => kind switch
    {
        EncoderKind.BarWindow => new BarWindowEncoder(),
        EncoderKind.VolumeProfile => new VolumeProfileEncoder(),
        EncoderKind.TimeProfile => new TimeProfileEncoder(),
        EncoderKind.VolumeProfileRaw => new VolumeProfileRawEncoder(),
        EncoderKind.TimeProfileRaw => new TimeProfileRawEncoder(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

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
}
