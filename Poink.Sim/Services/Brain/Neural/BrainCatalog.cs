using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

public enum EncoderKind
{
	BarWindow,
	VolumeProfile,
	TimeProfile,
	SessionClock,
	SpreadVolatility,
	TraderState,
	XGrid,
}

public enum ArchitectureKind
{
	Mlp,
	Lstm,
	Cnn,
	Neat,
}

public enum NormalizationKind
{
	Normalized,
	Raw,
	LocalZScore,
	GlobalZScore,
	Binary,
}

public enum ActionOutputKind
{
	Volume,
	StopLoss,
	TakeProfit,
}

public enum ActionOutputEncodingKind
{
	DiscretizedLevels,
}

public enum DecisionDecodingKind
{
	Softmax,
	HardMax,
}

public enum CnnKernelSweepSource
{
	BarWindow,
	VolumeProfile,
}

public enum DirectionMaskRuleKind
{
	PositionRequiredForClose,
	FlatRequiredForEntry,
	LongRequiresUpSignal,
	ShortRequiresDownSignal,
	KeyLevelRequiredForClose,
}

public enum TraderStateFieldKind
{
	Position,
	PositionPrice,
	PositionTicks,
	PositionDistance,
	CurrentPrice,
	Cash,
	Realized,
	Unrealized,
	Equity,
	Margin,
	MarginLevel,
	PositionCount,
}

// XGridRowCount is now a row-height multiplier (row height = XGridRowCount x XGridPipSize),
// kept under its old name so saved brain JSON still deserializes onto the right field.
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
	bool TraderStateIncludePosition = true,
	bool TraderStateIncludePositionPrice = true,
	bool TraderStateIncludePositionTicks = true,
	bool TraderStateIncludePositionDistance = true,
	bool TraderStateIncludeCurrentPrice = true,
	bool TraderStateIncludeCash = true,
	bool TraderStateIncludeRealized = true,
	bool TraderStateIncludeUnrealized = true,
	bool TraderStateIncludeEquity = true,
	bool TraderStateIncludeMargin = true,
	bool TraderStateIncludeMarginLevel = true,
	bool TraderStateIncludePositionCount = true,
	// Which XGridSignalCatalog.All entries are enabled, by Key. Unknown or stale keys are
	// dropped rather than throwing (see BrainCatalog.ResolveXGridSignals).
	IReadOnlyList<string>? XGridEnabledSignals = null,
	double XGridTopPercent = 10,
	int XGridColCount = 20,
	int XGridRowCount = 20,
	double XGridPipSize = 0.01,
	int XGridWindowBars = 60
);

public sealed record BrainRecipe(
	IReadOnlyList<EncoderKind> EncoderKinds,
	ArchitectureKind ArchitectureKind,
	IReadOnlyList<int> HiddenSizes,
	IReadOnlyList<ActionOutputKind>? ActionOutputKinds = null,
	ActionOutputEncodingKind ActionOutputEncodingKind = ActionOutputEncodingKind.DiscretizedLevels,
	IReadOnlyList<SimulationAction>? EnabledDirections = null,
	EncoderSizeOptions? EncoderSizes = null,
	DecisionDecodingKind DecisionDecoding = DecisionDecodingKind.Softmax,
	double DecisionTemperature = 1.0,
	CnnKernelSweepSource CnnKernelSweepSource = CnnKernelSweepSource.BarWindow,
	IReadOnlyList<DirectionMaskRuleKind>? EnabledMaskRules = null,
	NeatHyperparameters? NeatHyperparameters = null
)
{
	public override string ToString()
	{
		string baseDescription =
			ArchitectureKind == ArchitectureKind.Neat
				? $"Neat[pop={(NeatHyperparameters ?? new NeatHyperparameters()).PopulationSize}] {string.Join("+", EncoderKinds)}"
				: $"{ArchitectureKind}[{string.Join(",", HiddenSizes)}] {string.Join("+", EncoderKinds)}";
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
		if (EnabledMaskRules is { } masks && masks.Count != BrainCatalog.AllDirectionMaskRuleKinds.Count)
			baseDescription += $" [masks {masks.Count}/{BrainCatalog.AllDirectionMaskRuleKinds.Count}]";
		return baseDescription;
	}
}

internal static class BrainCatalog
{
	public static readonly IReadOnlyList<DirectionMaskRuleKind> AllDirectionMaskRuleKinds =
	[
		DirectionMaskRuleKind.PositionRequiredForClose,
		DirectionMaskRuleKind.FlatRequiredForEntry,
		DirectionMaskRuleKind.LongRequiresUpSignal,
		DirectionMaskRuleKind.ShortRequiresDownSignal,
		DirectionMaskRuleKind.KeyLevelRequiredForClose,
	];

	// xGrid-backed rules need this brain's own XGridSignalEncoder instance to stay in sync with
	// what it reports as input features; a brain built without one simply can't evaluate them.
	public static IReadOnlyList<IDirectionMaskRule> CreateDirectionMaskRules(
		IReadOnlyList<DirectionMaskRuleKind> kinds,
		IReadOnlyList<IObservationEncoder> parts
	)
	{
		var xGridEncoder = parts.OfType<XGridSignalEncoder>().FirstOrDefault();
		var rules = new List<IDirectionMaskRule>();
		foreach (var kind in kinds)
		{
			IDirectionMaskRule? rule = kind switch
			{
				DirectionMaskRuleKind.PositionRequiredForClose => new PositionRequiredForCloseRule(),
				DirectionMaskRuleKind.FlatRequiredForEntry => new FlatRequiredForEntryRule(),
				DirectionMaskRuleKind.LongRequiresUpSignal => xGridEncoder is null
					? null
					: new DirectionalXGridSignalRule(
						SimulationAction.EnterLong,
						XGridSignalCatalog.Keys.RankTopPercentUp,
						xGridEncoder
					),
				DirectionMaskRuleKind.ShortRequiresDownSignal => xGridEncoder is null
					? null
					: new DirectionalXGridSignalRule(
						SimulationAction.EnterShort,
						XGridSignalCatalog.Keys.RankTopPercentDown,
						xGridEncoder
					),
				DirectionMaskRuleKind.KeyLevelRequiredForClose => xGridEncoder is null
					? null
					: new KeyLevelRequiredForCloseRule(xGridEncoder),
				_ => throw new ArgumentOutOfRangeException(nameof(kinds), kind, null),
			};
			if (rule is not null)
				rules.Add(rule);
		}
		return rules;
	}

	public static IObservationEncoder CreateEncoder(EncoderKind kind, EncoderSizeOptions? sizeOptions = null)
	{
		var sizes = sizeOptions ?? new EncoderSizeOptions();
		return kind switch
		{
			EncoderKind.BarWindow => new BarWindowEncoder(
				windowSize: sizes.BarWindowSize,
				normalization: sizes.BarWindowNormalization,
				zScoreSubWindowSize: sizes.BarWindowZScoreSubWindowSize
			),
			EncoderKind.VolumeProfile => new VolumeProfileEncoder(
				bucketCount: sizes.VolumeProfileBucketCount,
				lookbackBars: sizes.VolumeProfileLookbackBars,
				normalization: sizes.VolumeProfileNormalization,
				splitUpDown: sizes.VolumeProfileSplitUpDown,
				zScoreSubWindowSize: sizes.VolumeProfileZScoreSubWindowSize
			),
			EncoderKind.TimeProfile => new TimeProfileEncoder(
				bucketCount: sizes.TimeProfileBucketCount,
				lookbackBars: sizes.TimeProfileLookbackBars,
				normalization: sizes.TimeProfileNormalization,
				splitUpDown: sizes.TimeProfileSplitUpDown
			),
			EncoderKind.SessionClock => new SessionClockEncoder(),
			EncoderKind.SpreadVolatility => new SpreadVolatilityEncoder(atrPeriod: sizes.SpreadVolatilityAtrPeriod),
			EncoderKind.TraderState => new TraderStateEncoder(
				enabledBaseFields: ResolveTraderStateFields(sizes),
				tradeOutcomeListLength: sizes.TraderStateTradeOutcomeListLength,
				positionLevelListLength: sizes.TraderStatePositionLevelListLength,
				directionListLength: sizes.TraderStateDirectionListLength
			),
			EncoderKind.XGrid => new XGridSignalEncoder(
				signals: ResolveXGridSignals(sizes),
				percent: sizes.XGridTopPercent,
				rowPips: sizes.XGridRowCount,
				pipSize: sizes.XGridPipSize,
				colCount: sizes.XGridColCount,
				windowBars: sizes.XGridWindowBars
			),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
		};
	}

	// Not private: TrainingPreview needs the same enabled-signal order the encoder builds its
	// Features/Encode array in, to label each recorded/live feature value.
	internal static List<string> ResolveXGridSignals(EncoderSizeOptions sizes) =>
		(sizes.XGridEnabledSignals ?? []).Where(XGridSignalCatalog.Contains).ToList();

	// Not private: TrainingPreview needs the same enabled-field order the encoder itself
	// builds its Features/Encode array in, to label each recorded/live feature value.
	internal static List<TraderStateFieldKind> ResolveTraderStateFields(EncoderSizeOptions sizes)
	{
		var fields = new List<TraderStateFieldKind>();
		if (sizes.TraderStateIncludePosition)
			fields.Add(TraderStateFieldKind.Position);
		if (sizes.TraderStateIncludePositionPrice)
			fields.Add(TraderStateFieldKind.PositionPrice);
		if (sizes.TraderStateIncludePositionTicks)
			fields.Add(TraderStateFieldKind.PositionTicks);
		if (sizes.TraderStateIncludePositionDistance)
			fields.Add(TraderStateFieldKind.PositionDistance);
		if (sizes.TraderStateIncludeCurrentPrice)
			fields.Add(TraderStateFieldKind.CurrentPrice);
		if (sizes.TraderStateIncludeCash)
			fields.Add(TraderStateFieldKind.Cash);
		if (sizes.TraderStateIncludeRealized)
			fields.Add(TraderStateFieldKind.Realized);
		if (sizes.TraderStateIncludeUnrealized)
			fields.Add(TraderStateFieldKind.Unrealized);
		if (sizes.TraderStateIncludeEquity)
			fields.Add(TraderStateFieldKind.Equity);
		if (sizes.TraderStateIncludeMargin)
			fields.Add(TraderStateFieldKind.Margin);
		if (sizes.TraderStateIncludeMarginLevel)
			fields.Add(TraderStateFieldKind.MarginLevel);
		if (sizes.TraderStateIncludePositionCount)
			fields.Add(TraderStateFieldKind.PositionCount);
		return fields;
	}

	public static INetworkArchitecture CreateArchitecture(ArchitectureKind kind, IReadOnlyList<int> hiddenSizes) =>
		kind switch
		{
			ArchitectureKind.Mlp => new MlpArchitecture([.. hiddenSizes]),
			ArchitectureKind.Lstm => new LstmArchitecture(
				hiddenSize: hiddenSizes.Count > 0 ? hiddenSizes[0] : 64,
				numLayers: hiddenSizes.Count > 1 ? hiddenSizes[1] : 2
			),
			ArchitectureKind.Cnn => new CnnArchitecture(
				kernelSize: hiddenSizes.Count > 0 ? hiddenSizes[0] : 3,
				channels: hiddenSizes.Count > 1 ? [.. hiddenSizes.Skip(1)] : [16, 32]
			),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
		};

	public static IActionOutputHead CreateActionOutputHead(
		ActionOutputKind kind,
		ActionOutputEncodingKind encodingKind,
		DecisionDecodingKind decoding = DecisionDecodingKind.Softmax,
		double temperature = 1.0
	) =>
		encodingKind switch
		{
			ActionOutputEncodingKind.DiscretizedLevels => new DiscretizedLevelsActionHead(kind, decoding, temperature),
			_ => throw new ArgumentOutOfRangeException(nameof(encodingKind), encodingKind, null),
		};
}
