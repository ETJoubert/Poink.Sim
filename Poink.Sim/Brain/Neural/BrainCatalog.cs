// ── UI-facing catalog of buildable encoder/architecture pieces ──────────
//  What the Train page's Brain Builder panel (see RlTrainingService.
//  TryBuildBrain / TrainPage.razor.cs) actually offers -- a plain enum-driven
//  factory rather than reflection/attribute scanning, since the set of
//  encoders/architectures is small and changes rarely; adding a new one here
//  plus a new enum case is the whole cost of making it pickable in the UI.
//
//  CapitalStateEncoder is deliberately NOT an EncoderKind option -- every
//  built brain needs account state regardless of how its market data is
//  represented, so RlTrainingService.TryBuildBrain appends it unconditionally
//  rather than letting a user omit it.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Brain.Neural;

internal enum EncoderKind { BarWindow, VolumeProfile, TimeProfile }

internal enum ArchitectureKind { Mlp, Lstm }

internal static class BrainCatalog
{
    public static IObservationEncoder CreateEncoder(EncoderKind kind) => kind switch
    {
        EncoderKind.BarWindow => new BarWindowEncoder(),
        EncoderKind.VolumeProfile => new VolumeProfileEncoder(),
        EncoderKind.TimeProfile => new TimeProfileEncoder(),
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
