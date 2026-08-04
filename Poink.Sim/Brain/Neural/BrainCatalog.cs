
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;


public enum EncoderKind { BarWindow, VolumeProfile, TimeProfile, SessionClock, SpreadVolatility, TraderState }

public enum ArchitectureKind { Mlp, Lstm, Cnn }

public enum NormalizationKind { Normalized, Raw, LocalZScore, GlobalZScore, Binary }


public enum ActionOutputKind { Volume, StopLoss, TakeProfit }


public enum ActionOutputEncodingKind { DiscretizedLevels }

public enum DecisionDecodingKind { Softmax, HardMax }


public enum CnnKernelSweepSource { BarWindow, VolumeProfile }


public sealed record EncoderSizeOptions(
    int BarWindowSize = 20,
    int VolumeProfileBucketCount = 20,
    int TimeProfileBucketCount = 20,
    int VolumeProfileLookbackBars = 50,
    int TimeProfileLookbackBars = 50,
    NormalizationKind BarWindowNormalization = NormalizationKind.Normalized,
    NormalizationKind VolumeProfileNormalization = NormalizationKind.Normalized,
    NormalizationKind TimeProfileNormalization = NormalizationKind.Normalized,
    int SpreadVolatilityAtrPeriod = 14,
    bool VolumeProfileSplitUpDown = false,
    bool TimeProfileSplitUpDown = false,
    int BarWindowZScoreSubWindowSize = 5,
    int VolumeProfileZScoreSubWindowSize = 5,
    int TraderStateTradeOutcomeListLength = 0,
    int TraderStatePositionLevelListLength = 0,
    int TraderStateDirectionListLength = 0);


public sealed record BrainRecipe(
    IReadOnlyList<EncoderKind> EncoderKinds, ArchitectureKind ArchitectureKind, IReadOnlyList<int> HiddenSizes,
    IReadOnlyList<ActionOutputKind>? ActionOutputKinds = null,
    ActionOutputEncodingKind ActionOutputEncodingKind = ActionOutputEncodingKind.DiscretizedLevels,
    IReadOnlyList<SimulationAction>? EnabledDirections = null,
    EncoderSizeOptions? EncoderSizes = null,
    DecisionDecodingKind DecisionDecoding = DecisionDecodingKind.Softmax,
    double DecisionTemperature = 1.0,
    CnnKernelSweepSource CnnKernelSweepSource = CnnKernelSweepSource.BarWindow)
{
    public override string ToString()
    {
        string baseDescription = $"{ArchitectureKind}[{string.Join(",", HiddenSizes)}] {string.Join("+", EncoderKinds)}";
        if (ArchitectureKind == ArchitectureKind.Cnn && CnnKernelSweepSource != CnnKernelSweepSource.BarWindow)
            baseDescription += $" [sweep={CnnKernelSweepSource}]";
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
            EncoderKind.BarWindow => new BarWindowEncoder(
                windowSize: sizes.BarWindowSize, normalization: sizes.BarWindowNormalization,
                zScoreSubWindowSize: sizes.BarWindowZScoreSubWindowSize),
            EncoderKind.VolumeProfile => new VolumeProfileEncoder(
                bucketCount: sizes.VolumeProfileBucketCount, lookbackBars: sizes.VolumeProfileLookbackBars, normalization: sizes.VolumeProfileNormalization,
                splitUpDown: sizes.VolumeProfileSplitUpDown, zScoreSubWindowSize: sizes.VolumeProfileZScoreSubWindowSize),
            EncoderKind.TimeProfile => new TimeProfileEncoder(
                bucketCount: sizes.TimeProfileBucketCount, lookbackBars: sizes.TimeProfileLookbackBars, normalization: sizes.TimeProfileNormalization,
                splitUpDown: sizes.TimeProfileSplitUpDown),
            EncoderKind.SessionClock => new SessionClockEncoder(),
            EncoderKind.SpreadVolatility => new SpreadVolatilityEncoder(atrPeriod: sizes.SpreadVolatilityAtrPeriod),
            EncoderKind.TraderState => new TraderStateEncoder(
                tradeOutcomeListLength: sizes.TraderStateTradeOutcomeListLength,
                positionLevelListLength: sizes.TraderStatePositionLevelListLength,
                directionListLength: sizes.TraderStateDirectionListLength),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }


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
