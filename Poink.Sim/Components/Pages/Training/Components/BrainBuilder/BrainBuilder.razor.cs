using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class BrainBuilder : ComponentBase
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Inject]
		private IBrainService BrainService { get; set; } = default!;

		[Parameter]
		public EventCallback<string> OnBrainCreated { get; set; }

		[Parameter]
		public string BrainName { get; set; } = "";

		[Parameter]
		public EventCallback<string> BrainNameChanged { get; set; }

		[Parameter]
		public string StrategyName { get; set; } = "";

		[Parameter]
		public EventCallback<string> StrategyNameChanged { get; set; }

		[Parameter]
		public double LearningRate { get; set; }

		[Parameter]
		public EventCallback<double> LearningRateChanged { get; set; }

		private IReadOnlyList<BrainOption> TrainableBrainOptions =>
			BrainService
				.Brains.Select(n => n.Name)
				.Concat(BrainService.DraftBrainNames)
				.Distinct()
				.Select(name => new BrainOption(name, true))
				.ToList();

		protected override void OnInitialized()
		{
			if (string.IsNullOrEmpty(BrainName) && TrainableBrainOptions.Count > 0)
				_ = BrainNameChanged.InvokeAsync(TrainableBrainOptions[0].Name);
			if (string.IsNullOrEmpty(StrategyName) && Service.StrategyNames.Count > 0)
				_ = StrategyNameChanged.InvokeAsync(Service.StrategyNames[0]);
		}

		private bool IsDraft =>
			PersistedRecipeForCurrentName is not { } persisted || !RecipesEqual(persisted, CurrentRecipe);

		private bool IsModifiedFromSaved =>
			PersistedRecipeForCurrentName is { } persisted && !RecipesEqual(persisted, CurrentRecipe);

		private BrainRecipe? PersistedRecipeForCurrentName =>
			BrainService.Brains.FirstOrDefault(b => b.Name == EffectiveBuildBrainName)?.Recipe;

		private bool BrainNameIsPersisted => BrainService.Brains.Any(b => b.Name == BrainName);

		private static bool RecipesEqual(BrainRecipe a, BrainRecipe b) =>
			a.ArchitectureKind == b.ArchitectureKind
			&& a.ActionOutputEncodingKind == b.ActionOutputEncodingKind
			&& a.DecisionDecoding == b.DecisionDecoding
			&& a.DecisionTemperature == b.DecisionTemperature
			&& a.CnnKernelSweepSource == b.CnnKernelSweepSource
			&& Equals(a.EncoderSizes, b.EncoderSizes)
			&& a.EncoderKinds.SequenceEqual(b.EncoderKinds)
			&& a.HiddenSizes.SequenceEqual(b.HiddenSizes)
			&& (a.ActionOutputKinds ?? []).SequenceEqual(b.ActionOutputKinds ?? [])
			&& (a.EnabledDirections ?? ComposedPolicyBrain.AllDirections).SequenceEqual(
				b.EnabledDirections ?? ComposedPolicyBrain.AllDirections
			)
			&& (a.EnabledMaskRules ?? BrainCatalog.AllDirectionMaskRuleKinds).SequenceEqual(
				b.EnabledMaskRules ?? BrainCatalog.AllDirectionMaskRuleKinds
			)
			&& a.DecisionMaskResolution == b.DecisionMaskResolution
			&& a.DecisionMaskResolutionSeconds == b.DecisionMaskResolutionSeconds
			&& a.DecisionMaskResolutionCellWidth == b.DecisionMaskResolutionCellWidth
			&& (a.EnabledDecisionMaskRules ?? []).SequenceEqual(b.EnabledDecisionMaskRules ?? [])
			&& Equals(a.NeatHyperparameters, b.NeatHyperparameters);

		private bool _showBuildPanel;

		private BrainBuilderTab _activeBuildTab;

		private enum BrainBuilderTab
		{
			Learn,
			Features,
			Decide,
			Struct,
			Actions,
		}

		private void StartNewBrain()
		{
			ResetBuildFieldsToDefaults();
			_showBuildPanel = true;
		}

		private void ModifyCurrentBrain()
		{
			if (!string.IsNullOrEmpty(BrainName) && BrainService.GetRecipe(BrainName) is { } recipe)
				LoadRecipe(BrainName, recipe);
			_showBuildPanel = true;
		}

		private bool _showDeleteConfirm;
		private string? _deleteStatus;

		private void RequestDeleteBrain() => _showDeleteConfirm = true;

		private void CancelDeleteBrain() => _showDeleteConfirm = false;

		private void ConfirmDeleteBrain()
		{
			_showDeleteConfirm = false;
			var toDelete = BrainService.Brains.FirstOrDefault(b => b.Name == BrainName);
			if (toDelete is null)
			{
				_deleteStatus = "brain no longer exists";
				return;
			}

			if (BrainService.DeleteNetwork(toDelete.Id))
			{
				_deleteStatus = $"deleted '{toDelete.Name}'";
				_ = BrainNameChanged.InvokeAsync(TrainableBrainOptions.FirstOrDefault()?.Name ?? "");
			}
			else
			{
				_deleteStatus = BrainService.Status;
			}
		}

		private Task OnBrainNameChanged(string name) => BrainNameChanged.InvokeAsync(name);

		private void LoadRecipe(string name, BrainRecipe recipe)
		{
			_buildBrainName = name;
			_buildBrainNameAuto = false;

			var state = new BrainBuilderState
			{
				IncludeBarWindow = recipe.EncoderKinds.Contains(EncoderKind.BarWindow),
				IncludeVolumeProfile = recipe.EncoderKinds.Contains(EncoderKind.VolumeProfile),
				IncludeTimeProfile = recipe.EncoderKinds.Contains(EncoderKind.TimeProfile),
				IncludeSessionClock = recipe.EncoderKinds.Contains(EncoderKind.SessionClock),
				IncludeSpreadVolatility = recipe.EncoderKinds.Contains(EncoderKind.SpreadVolatility),
				IncludeTraderState = recipe.EncoderKinds.Contains(EncoderKind.TraderState),
			};

			var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
			state.BarWindowNormalization = sizes.BarWindowNormalization;
			state.VolumeProfileNormalization = sizes.VolumeProfileNormalization;
			state.TimeProfileNormalization = sizes.TimeProfileNormalization;
			state.VolumeProfileSplitUpDown = sizes.VolumeProfileSplitUpDown;
			state.TimeProfileSplitUpDown = sizes.TimeProfileSplitUpDown;
			state.BarWindowSize = sizes.BarWindowSize;
			state.VolumeProfileBucketCount = sizes.VolumeProfileBucketCount;
			state.TimeProfileBucketCount = sizes.TimeProfileBucketCount;
			state.VolumeProfileLookbackBars = sizes.VolumeProfileLookbackBars;
			state.TimeProfileLookbackBars = sizes.TimeProfileLookbackBars;
			state.SpreadVolatilityAtrPeriod = sizes.SpreadVolatilityAtrPeriod;
			state.BarWindowZScoreSubWindowSize = sizes.BarWindowZScoreSubWindowSize;
			state.VolumeProfileZScoreSubWindowSize = sizes.VolumeProfileZScoreSubWindowSize;
			state.IncludeTraderStateOutcomes = sizes.TraderStateTradeOutcomeListLength > 0;
			state.IncludeTraderStatePositionLevel = sizes.TraderStatePositionLevelListLength > 0;
			state.IncludeTraderStateDirection = sizes.TraderStateDirectionListLength > 0;
			state.TraderStateTradeOutcomeListLength =
				sizes.TraderStateTradeOutcomeListLength > 0
					? sizes.TraderStateTradeOutcomeListLength
					: _state.TraderStateTradeOutcomeListLength;
			state.TraderStatePositionLevelListLength =
				sizes.TraderStatePositionLevelListLength > 0
					? sizes.TraderStatePositionLevelListLength
					: _state.TraderStatePositionLevelListLength;
			state.TraderStateDirectionListLength =
				sizes.TraderStateDirectionListLength > 0
					? sizes.TraderStateDirectionListLength
					: _state.TraderStateDirectionListLength;

			state.TraderStateIncludePosition = sizes.TraderStateIncludePosition;
			state.TraderStateIncludePositionPrice = sizes.TraderStateIncludePositionPrice;
			state.TraderStateIncludePositionTicks = sizes.TraderStateIncludePositionTicks;
			state.TraderStateIncludePositionDistance = sizes.TraderStateIncludePositionDistance;
			state.TraderStateIncludeCurrentPrice = sizes.TraderStateIncludeCurrentPrice;
			state.TraderStateIncludeCash = sizes.TraderStateIncludeCash;
			state.TraderStateIncludeRealized = sizes.TraderStateIncludeRealized;
			state.TraderStateIncludeUnrealized = sizes.TraderStateIncludeUnrealized;
			state.TraderStateIncludeEquity = sizes.TraderStateIncludeEquity;
			state.TraderStateIncludeMargin = sizes.TraderStateIncludeMargin;
			state.TraderStateIncludeMarginLevel = sizes.TraderStateIncludeMarginLevel;
			state.TraderStateIncludePositionCount = sizes.TraderStateIncludePositionCount;

			state.XGridEnabledSignals = sizes.XGridEnabledSignals is { } enabled ? new HashSet<string>(enabled) : [];
			state.XGridTopPercent = sizes.XGridTopPercent;
			state.XGridColCount = sizes.XGridColCount;
			state.XGridRowCount = sizes.XGridRowCount;
			state.XGridPipSize = sizes.XGridPipSize;
			state.XGridWindowBars = sizes.XGridWindowBars;

			state.ArchitectureKind = recipe.ArchitectureKind;
			switch (recipe.ArchitectureKind)
			{
				case ArchitectureKind.Cnn:
					state.CnnKernelSize = recipe.HiddenSizes.Count > 0 ? recipe.HiddenSizes[0] : 3;
					state.CnnChannels = recipe.HiddenSizes.Count > 1 ? recipe.HiddenSizes.Skip(1).ToList() : [16, 32];
					state.CnnKernelSweepSource = recipe.CnnKernelSweepSource;
					break;
				case ArchitectureKind.Lstm:
					state.LstmHiddenSize = recipe.HiddenSizes.Count > 0 ? recipe.HiddenSizes[0] : 64;
					state.LstmNumLayers = recipe.HiddenSizes.Count > 1 ? recipe.HiddenSizes[1] : 2;
					break;
				case ArchitectureKind.Neat:
					var h = recipe.NeatHyperparameters ?? new NeatHyperparameters();
					state.NeatPopulationSize = h.PopulationSize;
					state.NeatCompatibilityThreshold = h.CompatibilityThreshold;
					state.NeatExcessCoefficient = h.ExcessCoefficient;
					state.NeatDisjointCoefficient = h.DisjointCoefficient;
					state.NeatWeightDiffCoefficient = h.WeightDiffCoefficient;
					state.NeatSurvivalThreshold = h.SurvivalThreshold;
					state.NeatWeightMutationRate = h.WeightMutationRate;
					state.NeatWeightResetChance = h.WeightResetChance;
					state.NeatWeightPerturbStdDev = h.WeightPerturbStdDev;
					state.NeatAddConnectionMutationRate = h.AddConnectionMutationRate;
					state.NeatAddNodeMutationRate = h.AddNodeMutationRate;
					state.NeatDisableInheritedGeneChance = h.DisableInheritedGeneChance;
					state.NeatInterspeciesMatingRate = h.InterspeciesMatingRate;
					state.NeatStagnationGenerations = h.StagnationGenerations;
					state.NeatElitismMinSpeciesSize = h.ElitismMinSpeciesSize;
					break;
				default:
					state.MlpHiddenSizes = recipe.HiddenSizes.Count > 0 ? recipe.HiddenSizes.ToList() : [64];
					break;
			}

			var actionKinds = recipe.ActionOutputKinds ?? [];
			state.IncludeVolumeOutput = actionKinds.Contains(ActionOutputKind.Volume);
			state.IncludeStopLossOutput = actionKinds.Contains(ActionOutputKind.StopLoss);
			state.IncludeTakeProfitOutput = actionKinds.Contains(ActionOutputKind.TakeProfit);

			var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
			state.IncludeHold = directions.Contains(SimulationAction.Hold);
			state.IncludeEnterLong = directions.Contains(SimulationAction.EnterLong);
			state.IncludeEnterShort = directions.Contains(SimulationAction.EnterShort);
			state.IncludeClose = directions.Contains(SimulationAction.Close);

			state.DecisionDecoding = recipe.DecisionDecoding;
			state.DecisionTemperature = recipe.DecisionTemperature;

			var masks = recipe.EnabledMaskRules ?? BrainCatalog.AllDirectionMaskRuleKinds;
			state.MaskPositionRequiredForClose = masks.Contains(DirectionMaskRuleKind.PositionRequiredForClose);
			state.MaskFlatRequiredForEntry = masks.Contains(DirectionMaskRuleKind.FlatRequiredForEntry);
			state.MaskLongRequiresUpSignal = masks.Contains(DirectionMaskRuleKind.LongRequiresUpSignal);
			state.MaskShortRequiresDownSignal = masks.Contains(DirectionMaskRuleKind.ShortRequiresDownSignal);
			state.MaskKeyLevelRequiredForClose = masks.Contains(DirectionMaskRuleKind.KeyLevelRequiredForClose);

			state.DecisionMaskResolution = recipe.DecisionMaskResolution;
			state.DecisionMaskResolutionSeconds = recipe.DecisionMaskResolutionSeconds;
			state.DecisionMaskResolutionCellWidth = recipe.DecisionMaskResolutionCellWidth;
			var decisionMasks = recipe.EnabledDecisionMaskRules ?? [];
			state.MaskDecisionPriceBelowPoc = decisionMasks.Contains(DecisionMaskRuleKind.PriceBelowPoc);

			_state = state;
			_buildBrainStatus = null;
		}

		private BrainBuilderState _state = new();

		// "+ NEW BRAIN" resets every field back to BrainBuilderState's own declared defaults.
		private void ResetBuildFieldsToDefaults()
		{
			_buildBrainName = "";
			_buildBrainNameAuto = true;
			_buildBrainStatus = null;
			_state = new BrainBuilderState();
		}

		private string _buildBrainName = "";

		private bool _buildBrainNameAuto = true;
		private string? _buildBrainStatus;

		private void SectionChanged() => StateHasChanged();

		private IReadOnlyList<int> EffectiveMlpHiddenSizes => _state.MlpHiddenSizes.Where(n => n > 0).ToList();

		private IReadOnlyList<int> EffectiveCnnChannels => _state.CnnChannels.Where(n => n > 0).ToList();

		private bool CnnSweepSourceIncluded =>
			_state.CnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? _state.IncludeVolumeProfile : _state.IncludeBarWindow;

		private bool AnyXGridSignalSelected => _state.XGridEnabledSignals.Count > 0;

		private bool CanCreateBrain =>
			!string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
			&& (
				_state.IncludeBarWindow
				|| _state.IncludeVolumeProfile
				|| _state.IncludeTimeProfile
				|| _state.IncludeSessionClock
				|| _state.IncludeSpreadVolatility
				|| _state.IncludeTraderState
				|| AnyXGridSignalSelected
			)
			&& (_state.IncludeHold || _state.IncludeEnterLong || _state.IncludeEnterShort || _state.IncludeClose)
			&& (_state.ArchitectureKind != ArchitectureKind.Lstm || _state.IncludeBarWindow)
			&& (
				_state.ArchitectureKind != ArchitectureKind.Cnn
				|| (CnnSweepSourceIncluded && _state.CnnKernelSize >= 1 && EffectiveCnnChannels.Count > 0)
			)
			&& (_state.ArchitectureKind != ArchitectureKind.Neat || _state.NeatPopulationSize >= 1)
			&& (!_state.IncludeBarWindow || _state.BarWindowSize >= 1)
			&& (!_state.IncludeVolumeProfile || (_state.VolumeProfileBucketCount >= 1 && _state.VolumeProfileLookbackBars >= 1))
			&& (!_state.IncludeTimeProfile || (_state.TimeProfileBucketCount >= 1 && _state.TimeProfileLookbackBars >= 1))
			&& (!_state.IncludeSpreadVolatility || _state.SpreadVolatilityAtrPeriod >= 1)
			&& (_state.BarWindowNormalization != NormalizationKind.LocalZScore || _state.BarWindowZScoreSubWindowSize >= 1)
			&& (_state.VolumeProfileNormalization != NormalizationKind.LocalZScore || _state.VolumeProfileZScoreSubWindowSize >= 1)
			&& (!_state.IncludeTraderStateOutcomes || _state.TraderStateTradeOutcomeListLength >= 1)
			&& (!_state.IncludeTraderStatePositionLevel || _state.TraderStatePositionLevelListLength >= 1)
			&& (!_state.IncludeTraderStateDirection || _state.TraderStateDirectionListLength >= 1)
			&& (
				!AnyXGridSignalSelected
				|| (
					_state.XGridColCount >= 1
					&& _state.XGridRowCount >= 1
					&& _state.XGridPipSize > 0
					&& _state.XGridWindowBars >= 1
					&& _state.XGridTopPercent is >= 0 and <= 100
				)
			)
			&& _state.DecisionTemperature > 0;

		private IReadOnlyList<EncoderKind> SelectedEncoderKinds
		{
			get
			{
				var kinds = new List<EncoderKind>();
				if (_state.IncludeBarWindow)
					kinds.Add(EncoderKind.BarWindow);
				if (_state.IncludeVolumeProfile)
					kinds.Add(EncoderKind.VolumeProfile);
				if (_state.IncludeTimeProfile)
					kinds.Add(EncoderKind.TimeProfile);
				if (_state.IncludeSessionClock)
					kinds.Add(EncoderKind.SessionClock);
				if (_state.IncludeSpreadVolatility)
					kinds.Add(EncoderKind.SpreadVolatility);
				if (_state.IncludeTraderState)
					kinds.Add(EncoderKind.TraderState);
				if (AnyXGridSignalSelected)
					kinds.Add(EncoderKind.XGrid);
				return kinds;
			}
		}

		private EncoderSizeOptions CurrentEncoderSizeOptions =>
			new(
				BarWindowSize: _state.BarWindowSize,
				VolumeProfileBucketCount: _state.VolumeProfileBucketCount,
				TimeProfileBucketCount: _state.TimeProfileBucketCount,
				VolumeProfileLookbackBars: _state.VolumeProfileLookbackBars,
				TimeProfileLookbackBars: _state.TimeProfileLookbackBars,
				BarWindowNormalization: _state.BarWindowNormalization,
				VolumeProfileNormalization: _state.VolumeProfileNormalization,
				TimeProfileNormalization: _state.TimeProfileNormalization,
				SpreadVolatilityAtrPeriod: _state.SpreadVolatilityAtrPeriod,
				VolumeProfileSplitUpDown: _state.VolumeProfileSplitUpDown,
				TimeProfileSplitUpDown: _state.TimeProfileSplitUpDown,
				BarWindowZScoreSubWindowSize: _state.BarWindowZScoreSubWindowSize,
				VolumeProfileZScoreSubWindowSize: _state.VolumeProfileZScoreSubWindowSize,
				TraderStateTradeOutcomeListLength: _state.IncludeTraderStateOutcomes ? _state.TraderStateTradeOutcomeListLength : 0,
				TraderStatePositionLevelListLength: _state.IncludeTraderStatePositionLevel
					? _state.TraderStatePositionLevelListLength
					: 0,
				TraderStateDirectionListLength: _state.IncludeTraderStateDirection ? _state.TraderStateDirectionListLength : 0,
				TraderStateIncludePosition: _state.TraderStateIncludePosition,
				TraderStateIncludePositionPrice: _state.TraderStateIncludePositionPrice,
				TraderStateIncludePositionTicks: _state.TraderStateIncludePositionTicks,
				TraderStateIncludePositionDistance: _state.TraderStateIncludePositionDistance,
				TraderStateIncludeCurrentPrice: _state.TraderStateIncludeCurrentPrice,
				TraderStateIncludeCash: _state.TraderStateIncludeCash,
				TraderStateIncludeRealized: _state.TraderStateIncludeRealized,
				TraderStateIncludeUnrealized: _state.TraderStateIncludeUnrealized,
				TraderStateIncludeEquity: _state.TraderStateIncludeEquity,
				TraderStateIncludeMargin: _state.TraderStateIncludeMargin,
				TraderStateIncludeMarginLevel: _state.TraderStateIncludeMarginLevel,
				TraderStateIncludePositionCount: _state.TraderStateIncludePositionCount,
				XGridEnabledSignals: _state.XGridEnabledSignals.ToList(),
				XGridTopPercent: _state.XGridTopPercent,
				XGridColCount: _state.XGridColCount,
				XGridRowCount: _state.XGridRowCount,
				XGridPipSize: _state.XGridPipSize,
				XGridWindowBars: _state.XGridWindowBars
			);

		private int TotalInputCount =>
			SelectedEncoderKinds.Sum(k => BrainCatalog.CreateEncoder(k, CurrentEncoderSizeOptions).FeatureCount);

		private int FeatureCountFor(EncoderKind kind) =>
			BrainCatalog.CreateEncoder(kind, CurrentEncoderSizeOptions).FeatureCount;

		private int BarWindowFeatureCount => FeatureCountFor(EncoderKind.BarWindow);
		private int VolumeProfileFeatureCount => FeatureCountFor(EncoderKind.VolumeProfile);
		private int TimeProfileFeatureCount => FeatureCountFor(EncoderKind.TimeProfile);
		private int SessionClockFeatureCount => FeatureCountFor(EncoderKind.SessionClock);
		private int SpreadVolatilityFeatureCount => FeatureCountFor(EncoderKind.SpreadVolatility);
		private int TraderStateFeatureCount => FeatureCountFor(EncoderKind.TraderState);

		private int TimeSeriesInputsFeatureCount =>
			(_state.IncludeBarWindow ? BarWindowFeatureCount : 0)
			+ (_state.IncludeVolumeProfile ? VolumeProfileFeatureCount : 0)
			+ (_state.IncludeTimeProfile ? TimeProfileFeatureCount : 0)
			+ (_state.IncludeSessionClock ? SessionClockFeatureCount : 0)
			+ (_state.IncludeSpreadVolatility ? SpreadVolatilityFeatureCount : 0);

		private const int TotalMaskRuleCount = 5;

		private List<DirectionMaskRuleKind> SelectedMaskRuleKinds
		{
			get
			{
				var kinds = new List<DirectionMaskRuleKind>();
				if (_state.MaskPositionRequiredForClose)
					kinds.Add(DirectionMaskRuleKind.PositionRequiredForClose);
				if (_state.MaskFlatRequiredForEntry)
					kinds.Add(DirectionMaskRuleKind.FlatRequiredForEntry);
				if (_state.MaskLongRequiresUpSignal)
					kinds.Add(DirectionMaskRuleKind.LongRequiresUpSignal);
				if (_state.MaskShortRequiresDownSignal)
					kinds.Add(DirectionMaskRuleKind.ShortRequiresDownSignal);
				if (_state.MaskKeyLevelRequiredForClose)
					kinds.Add(DirectionMaskRuleKind.KeyLevelRequiredForClose);
				return kinds;
			}
		}

		private List<DecisionMaskRuleKind> SelectedDecisionMaskRuleKinds
		{
			get
			{
				var kinds = new List<DecisionMaskRuleKind>();
				if (_state.MaskDecisionPriceBelowPoc)
					kinds.Add(DecisionMaskRuleKind.PriceBelowPoc);
				return kinds;
			}
		}

		private NeatHyperparameters CurrentNeatHyperparameters =>
			new(
				_state.NeatPopulationSize,
				_state.NeatCompatibilityThreshold,
				_state.NeatExcessCoefficient,
				_state.NeatDisjointCoefficient,
				_state.NeatWeightDiffCoefficient,
				_state.NeatSurvivalThreshold,
				_state.NeatWeightMutationRate,
				_state.NeatWeightResetChance,
				_state.NeatWeightPerturbStdDev,
				_state.NeatAddConnectionMutationRate,
				_state.NeatAddNodeMutationRate,
				_state.NeatDisableInheritedGeneChance,
				_state.NeatInterspeciesMatingRate,
				_state.NeatStagnationGenerations,
				_state.NeatElitismMinSpeciesSize
			);

		private IReadOnlyList<int> CurrentHiddenSizes =>
			_state.ArchitectureKind switch
			{
				ArchitectureKind.Mlp => EffectiveMlpHiddenSizes,
				ArchitectureKind.Cnn => [_state.CnnKernelSize, .. EffectiveCnnChannels],
				ArchitectureKind.Neat => [],
				_ => [_state.LstmHiddenSize, _state.LstmNumLayers],
			};

		private List<ActionOutputKind> SelectedActionOutputKinds
		{
			get
			{
				var kinds = new List<ActionOutputKind>();
				if (_state.IncludeVolumeOutput)
					kinds.Add(ActionOutputKind.Volume);
				if (_state.IncludeStopLossOutput)
					kinds.Add(ActionOutputKind.StopLoss);
				if (_state.IncludeTakeProfitOutput)
					kinds.Add(ActionOutputKind.TakeProfit);
				return kinds;
			}
		}

		private List<SimulationAction> SelectedEnabledDirections
		{
			get
			{
				var directions = new List<SimulationAction>();
				if (_state.IncludeHold)
					directions.Add(SimulationAction.Hold);
				if (_state.IncludeEnterLong)
					directions.Add(SimulationAction.EnterLong);
				if (_state.IncludeEnterShort)
					directions.Add(SimulationAction.EnterShort);
				if (_state.IncludeClose)
					directions.Add(SimulationAction.Close);
				return directions;
			}
		}

		private BrainRecipe CurrentRecipe =>
			new(
				SelectedEncoderKinds.ToList(),
				_state.ArchitectureKind,
				CurrentHiddenSizes.ToList(),
				SelectedActionOutputKinds.Count > 0 ? SelectedActionOutputKinds : null,
				ActionOutputEncodingKind.DiscretizedLevels,
				SelectedEnabledDirections.Count > 0 ? SelectedEnabledDirections : null,
				CurrentEncoderSizeOptions,
				_state.DecisionDecoding,
				_state.DecisionTemperature,
				_state.CnnKernelSweepSource,
				SelectedMaskRuleKinds,
				CurrentNeatHyperparameters,
				_state.DecisionMaskResolution,
				_state.DecisionMaskResolutionSeconds,
				_state.DecisionMaskResolutionCellWidth,
				SelectedDecisionMaskRuleKinds
			);

		public BrainRecipe CurrentConfigRecipe => CurrentRecipe;

		private string AutoBuildBrainName
		{
			get
			{
				string arch = _state.ArchitectureKind switch
				{
					ArchitectureKind.Mlp => "MLP",
					ArchitectureKind.Lstm => "LSTM",
					_ => _state.ArchitectureKind.ToString().ToUpperInvariant(),
				};

				string layers = _state.ArchitectureKind switch
				{
					ArchitectureKind.Mlp => string.Join("-", EffectiveMlpHiddenSizes.Select(n => $"L{n}")),
					ArchitectureKind.Cnn => $"K{_state.CnnKernelSize}-"
						+ string.Join("-", EffectiveCnnChannels.Select(n => $"C{n}"))
						+ (_state.CnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? "-sweepVP" : ""),
					_ => $"H{_state.LstmHiddenSize}-N{_state.LstmNumLayers}",
				};

				var dataParts = new List<string>();
				if (_state.IncludeBarWindow)
					dataParts.Add(NormalizationTag(_state.BarWindowNormalization, "BW"));
				if (_state.IncludeVolumeProfile)
				{
					string vpTag = NormalizationTag(_state.VolumeProfileNormalization, "VP");
					dataParts.Add(_state.VolumeProfileSplitUpDown ? $"{vpTag}-split" : vpTag);
				}
				if (_state.IncludeTimeProfile)
				{
					string tpTag = _state.TimeProfileNormalization == NormalizationKind.Raw ? "dTPR" : "dTP";
					dataParts.Add(_state.TimeProfileSplitUpDown ? $"{tpTag}-split" : tpTag);
				}
				if (_state.IncludeSessionClock)
					dataParts.Add("dSC");
				if (_state.IncludeSpreadVolatility)
					dataParts.Add("dSV");
				if (_state.IncludeTraderState)
					dataParts.Add("dAS");
				if (AnyXGridSignalSelected)
					dataParts.Add("dXG");
				string data = string.Join("-", dataParts);

				string name = string.IsNullOrEmpty(layers) ? $"{arch}_{data}" : $"{arch}_{layers}_{data}";

				var actionParts = new List<string>();
				if (!_state.IncludeHold)
					actionParts.Add("noHOLD");
				if (!_state.IncludeEnterLong)
					actionParts.Add("noLONG");
				if (!_state.IncludeEnterShort)
					actionParts.Add("noSHORT");
				if (!_state.IncludeClose)
					actionParts.Add("noCLOSE");
				if (_state.IncludeVolumeOutput)
					actionParts.Add("aVOL");
				if (_state.IncludeStopLossOutput)
					actionParts.Add("aSL");
				if (_state.IncludeTakeProfitOutput)
					actionParts.Add("aTP");
				if (_state.DecisionDecoding == DecisionDecodingKind.HardMax)
					actionParts.Add("hardmax");
				else if (_state.DecisionTemperature != 1.0)
					actionParts.Add($"T{_state.DecisionTemperature:0.##}");
				if (SelectedMaskRuleKinds.Count != TotalMaskRuleCount)
					actionParts.Add($"mask{SelectedMaskRuleKinds.Count}");
				return actionParts.Count == 0 ? name : $"{name}_{string.Join("-", actionParts)}";
			}
		}

		private static string NormalizationTag(NormalizationKind kind, string setPrefix) =>
			kind switch
			{
				NormalizationKind.Raw => $"d{setPrefix}R",
				NormalizationKind.LocalZScore => $"d{setPrefix}ZL",
				NormalizationKind.GlobalZScore => $"d{setPrefix}ZG",
				NormalizationKind.Binary => $"d{setPrefix}B",
				_ => $"d{setPrefix}",
			};

		private string EffectiveBuildBrainName => _buildBrainNameAuto ? AutoBuildBrainName : _buildBrainName;

		private void OnBuildBrainNameInput(ChangeEventArgs e)
		{
			_buildBrainNameAuto = false;
			_buildBrainName = e.Value?.ToString() ?? "";
		}

		private void OnToggleAutoBuildBrainName(ChangeEventArgs e)
		{
			_buildBrainNameAuto = e.Value is bool isAuto && isAuto;
			if (!_buildBrainNameAuto)
				_buildBrainName = AutoBuildBrainName;
		}

		private void SaveBrain()
		{
			var name = EffectiveBuildBrainName;

			if (
				BrainService.TryBuildBrain(
					name,
					SelectedEncoderKinds,
					_state.ArchitectureKind,
					CurrentHiddenSizes,
					SelectedActionOutputKinds,
					SelectedEnabledDirections,
					CurrentEncoderSizeOptions,
					_state.DecisionDecoding,
					_state.DecisionTemperature,
					_state.CnnKernelSweepSource,
					SelectedMaskRuleKinds,
					CurrentNeatHyperparameters,
					_state.DecisionMaskResolution,
					_state.DecisionMaskResolutionSeconds,
					_state.DecisionMaskResolutionCellWidth,
					SelectedDecisionMaskRuleKinds
				)
			)
			{
				_buildBrainStatus = $"saved '{name}' to storage";
				_ = OnBrainCreated.InvokeAsync(name);
				_ = BrainNameChanged.InvokeAsync(name);
			}
			else
			{
				_buildBrainStatus = BrainService.Status;
			}
		}

		private bool TryDraftCurrentConfig()
		{
			if (!CanCreateBrain)
			{
				_buildBrainStatus = "fix the build panel before using this config";
				return false;
			}

			if (
				!BrainService.TryBuildDraftBrain(
					EffectiveBuildBrainName,
					SelectedEncoderKinds,
					_state.ArchitectureKind,
					CurrentHiddenSizes,
					SelectedActionOutputKinds,
					SelectedEnabledDirections,
					CurrentEncoderSizeOptions,
					_state.DecisionDecoding,
					_state.DecisionTemperature,
					_state.CnnKernelSweepSource,
					SelectedMaskRuleKinds,
					CurrentNeatHyperparameters,
					_state.DecisionMaskResolution,
					_state.DecisionMaskResolutionSeconds,
					_state.DecisionMaskResolutionCellWidth,
					SelectedDecisionMaskRuleKinds
				)
			)
			{
				_buildBrainStatus = BrainService.Status;
				return false;
			}

			_buildBrainStatus = null;
			return true;
		}

		private void SetConfig() => TryDraftCurrentConfig();

		public string? PrepareBrainForTraining()
		{
			if (!_showBuildPanel)
				return BrainName;
			if (!IsDraft)
				return EffectiveBuildBrainName;
			return TryDraftCurrentConfig() ? EffectiveBuildBrainName : null;
		}
	}
}
