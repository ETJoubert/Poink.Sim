
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;


public enum EncoderKind { BarWindow, VolumeProfile, TimeProfile, SessionClock, SpreadVolatility, TraderState, XGrid }

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
    int TraderStateDirectionListLength = 0,
    bool XGridRankTopTotal = false,
    bool XGridRankTopUp = false,
    bool XGridRankTopDown = false,
    bool XGridAtPoc = false,
    bool XGridAtVah = false,
    bool XGridAtVal = false,
    bool XGridAtVwap = false,
    bool XGridAbovePoc = false,
    bool XGridAboveVah = false,
    bool XGridAboveVal = false,
    bool XGridAboveVwap = false,
    bool XGridBelowPoc = false,
    bool XGridBelowVah = false,
    bool XGridBelowVal = false,
    bool XGridBelowVwap = false,
    double XGridTopPercent = 10,
    int XGridColCount = 20,
    int XGridRowCount = 20,
    int XGridWindowBars = 60);


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
            EncoderKind.XGrid => new XGridSignalEncoder(
                signals: ResolveXGridSignals(sizes), percent: sizes.XGridTopPercent,
                rowCount: sizes.XGridRowCount, colCount: sizes.XGridColCount, windowBars: sizes.XGridWindowBars),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // Not private: TrainingPreview needs the same enabled-signal order the encoder itself
    // builds its Features/Encode array in, to label each recorded/live feature value.
    internal static List<XGridSignalKind> ResolveXGridSignals(EncoderSizeOptions sizes)
    {
        var signals = new List<XGridSignalKind>();
        if (sizes.XGridRankTopTotal) signals.Add(XGridSignalKind.RankTopPercentTotal);
        if (sizes.XGridRankTopUp) signals.Add(XGridSignalKind.RankTopPercentUp);
        if (sizes.XGridRankTopDown) signals.Add(XGridSignalKind.RankTopPercentDown);
        if (sizes.XGridAtPoc) signals.Add(XGridSignalKind.AtPoc);
        if (sizes.XGridAtVah) signals.Add(XGridSignalKind.AtVah);
        if (sizes.XGridAtVal) signals.Add(XGridSignalKind.AtVal);
        if (sizes.XGridAtVwap) signals.Add(XGridSignalKind.AtVwap);
        if (sizes.XGridAbovePoc) signals.Add(XGridSignalKind.AbovePoc);
        if (sizes.XGridAboveVah) signals.Add(XGridSignalKind.AboveVah);
        if (sizes.XGridAboveVal) signals.Add(XGridSignalKind.AboveVal);
        if (sizes.XGridAboveVwap) signals.Add(XGridSignalKind.AboveVwap);
        if (sizes.XGridBelowPoc) signals.Add(XGridSignalKind.BelowPoc);
        if (sizes.XGridBelowVah) signals.Add(XGridSignalKind.BelowVah);
        if (sizes.XGridBelowVal) signals.Add(XGridSignalKind.BelowVal);
        if (sizes.XGridBelowVwap) signals.Add(XGridSignalKind.BelowVwap);
        return signals;
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
