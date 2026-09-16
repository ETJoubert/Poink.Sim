using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;

namespace Poink.Sim.Components.Training
{
	// Shared, mutable view-model for BrainBuilder's Section components -- field defaults here
	// are the single source of truth for "+ NEW BRAIN", which just assigns a new instance.
	public sealed class BrainBuilderState
	{
		public bool IncludeBarWindow = true;
		public bool IncludeVolumeProfile;
		public bool IncludeTimeProfile;
		public bool IncludeSessionClock;
		public bool IncludeSpreadVolatility;
		public bool IncludeTraderState;

		public NormalizationKind BarWindowNormalization = NormalizationKind.Normalized;
		public NormalizationKind VolumeProfileNormalization = NormalizationKind.Normalized;
		public NormalizationKind TimeProfileNormalization = NormalizationKind.Normalized;

		public bool VolumeProfileSplitUpDown;
		public bool TimeProfileSplitUpDown;

		public int BarWindowSize = 20;
		public int VolumeProfileBucketCount = 20;
		public int TimeProfileBucketCount = 20;
		public int VolumeProfileLookbackBars = 50;
		public int TimeProfileLookbackBars = 50;
		public int SpreadVolatilityAtrPeriod = 14;
		public int BarWindowZScoreSubWindowSize = 5;
		public int VolumeProfileZScoreSubWindowSize = 5;

		public bool IncludeTraderStateOutcomes;
		public bool IncludeTraderStatePositionLevel;
		public bool IncludeTraderStateDirection;

		public int TraderStateTradeOutcomeListLength = 5;
		public int TraderStatePositionLevelListLength = 5;
		public int TraderStateDirectionListLength = 5;

		public bool TraderStateIncludePosition = true;
		public bool TraderStateIncludePositionPrice = true;
		public bool TraderStateIncludePositionTicks = true;
		public bool TraderStateIncludePositionDistance = true;
		public bool TraderStateIncludeCurrentPrice = true;
		public bool TraderStateIncludeCash = true;
		public bool TraderStateIncludeRealized = true;
		public bool TraderStateIncludeUnrealized = true;
		public bool TraderStateIncludeEquity = true;
		public bool TraderStateIncludeMargin = true;
		public bool TraderStateIncludeMarginLevel = true;
		public bool TraderStateIncludePositionCount = true;

		public HashSet<string> XGridEnabledSignals = [];
		public double XGridTopPercent = 10;
		public int XGridColCount = 20;
		public int XGridRowCount = 20;
		public double XGridPipSize = 0.01;
		public int XGridWindowBars = 60;

		public ArchitectureKind ArchitectureKind = ArchitectureKind.Mlp;

		public List<int> MlpHiddenSizes = [64];
		public int LstmHiddenSize = 64;
		public int LstmNumLayers = 2;

		public int CnnKernelSize = 3;
		public List<int> CnnChannels = [16, 32];
		public CnnKernelSweepSource CnnKernelSweepSource = CnnKernelSweepSource.BarWindow;

		public int NeatPopulationSize = 150;
		public double NeatCompatibilityThreshold = 3.0;
		public double NeatExcessCoefficient = 1.0;
		public double NeatDisjointCoefficient = 1.0;
		public double NeatWeightDiffCoefficient = 0.4;
		public double NeatSurvivalThreshold = 0.2;
		public double NeatWeightMutationRate = 0.8;
		public double NeatWeightResetChance = 0.1;
		public double NeatWeightPerturbStdDev = 0.5;
		public double NeatAddConnectionMutationRate = 0.05;
		public double NeatAddNodeMutationRate = 0.03;
		public double NeatDisableInheritedGeneChance = 0.75;
		public double NeatInterspeciesMatingRate = 0.001;
		public int NeatStagnationGenerations = 15;
		public int NeatElitismMinSpeciesSize = 5;

		public bool IncludeVolumeOutput;
		public bool IncludeStopLossOutput;
		public bool IncludeTakeProfitOutput;

		public bool IncludeHold = true;
		public bool IncludeEnterLong = true;
		public bool IncludeEnterShort = true;
		public bool IncludeClose = true;

		public DecisionDecodingKind DecisionDecoding = DecisionDecodingKind.Softmax;
		public double DecisionTemperature = 1.0;

		public bool MaskPositionRequiredForClose = true;
		public bool MaskFlatRequiredForEntry = true;
		public bool MaskLongRequiresUpSignal = true;
		public bool MaskShortRequiresDownSignal = true;
		public bool MaskKeyLevelRequiredForClose = true;

		public DecisionMaskResolutionKind DecisionMaskResolution = DecisionMaskResolutionKind.PerBar;
		public double DecisionMaskResolutionSeconds = 1.0;
		public double DecisionMaskResolutionCellWidth = 0.10;
		public bool MaskDecisionPriceBelowPoc;
	}
}
